using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartFacility.Application.Analytics.Abstractions;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Domain;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Analytics;

public sealed class EfAnalyticsQueryService(SmartFacilityDbContext dbContext) :
    IAssetAnalyticsService,
    IWorkOrderAnalyticsService,
    IWorkOrderActivityService,
    IScadaAnalyticsService,
    IImportQualityAnalyticsService
{
    private const string UnknownCategory = "<Unknown>";
    private const string LegacyFingerprintAlgorithm = "<Legacy>";
    private const string TimeZoneAssumption = "UnspecifiedSourceLocal";

    private const string ScadaClearanceStatisticsSql = """
        WITH Matched AS
        (
            SELECT
                [ReceivedAt],
                [ClearedAt],
                [DateTimeParseStatus]
            FROM [core].[ScadaAlarmEvents]
            WHERE
                (@dateFrom IS NULL OR [ReceivedAt] >= @dateFrom)
                AND (@dateToExclusive IS NULL OR [ReceivedAt] < @dateToExclusive)
                AND (@sourceSheet IS NULL OR [SourceSheet] = @sourceSheet)
                AND (@alarmType IS NULL OR [AlarmType] = @alarmType)
                AND (@interventionLevel IS NULL OR [InterventionLevel] = @interventionLevel)
                AND (@section IS NULL OR [SectionRaw] = @section)
                AND (@locationRaw IS NULL OR [LocationRaw] = @locationRaw)
        ),
        Eligible AS
        (
            SELECT
                [ReceivedAt],
                DATEDIFF_BIG(millisecond, [ReceivedAt], [ClearedAt]) / 60000.0
                    AS [DurationMinutes]
            FROM Matched
            WHERE
                [ReceivedAt] IS NOT NULL
                AND [ClearedAt] IS NOT NULL
                AND [DateTimeParseStatus] IS NOT NULL
                AND [DateTimeParseStatus] LIKE N'%Received:Parsed%'
                AND [DateTimeParseStatus] LIKE N'%Cleared:Parsed%'
                AND [ClearedAt] >= [ReceivedAt]
                AND [ReceivedAt] < @sourceDateExclusive
                AND [ClearedAt] < @sourceDateExclusive
                AND [DateTimeParseStatus] NOT LIKE N'%InvalidDate%'
                AND [DateTimeParseStatus] NOT LIKE N'%InvalidTime%'
                AND [DateTimeParseStatus] NOT LIKE N'%DateOnlySource%'
                AND [DateTimeParseStatus] NOT LIKE N'%PlaceholderX%'
                AND [DateTimeParseStatus] NOT LIKE N'%SuspiciousYear%'
                AND [DateTimeParseStatus] NOT LIKE N'%FutureDate%'
                AND [DateTimeParseStatus] NOT LIKE N'%ClearedBeforeReceived%'
        ),
        Percentiles AS
        (
            SELECT
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY [DurationMinutes]) OVER ()
                    AS [MedianMinutes],
                PERCENTILE_CONT(0.90) WITHIN GROUP (ORDER BY [DurationMinutes]) OVER ()
                    AS [P90Minutes]
            FROM Eligible
        )
        SELECT
            (SELECT COUNT_BIG(*) FROM Matched) AS [TotalMatchedOccurrences],
            (SELECT COUNT_BIG(*) FROM Eligible) AS [EligibleOccurrences],
            (SELECT MIN([ReceivedAt]) FROM Eligible) AS [ActualMinDate],
            (SELECT MAX([ReceivedAt]) FROM Eligible) AS [ActualMaxDate],
            CAST(MAX([MedianMinutes]) AS decimal(18,2)) AS [MedianMinutes],
            CAST(MAX([P90Minutes]) AS decimal(18,2)) AS [P90Minutes]
        FROM Percentiles
        """;

    private readonly SmartFacilityDbContext _dbContext = dbContext;

    public async Task<AssetOverviewResponse> GetOverviewAsync(
        AssetOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var assets = ApplyAssetFilters(_dbContext.Assets.AsNoTracking(), query);
        var totalAssetCount = await assets.LongCountAsync(cancellationToken);

        var countByBuilding = await assets
            .GroupBy(asset => new
            {
                asset.BuildingId,
                Name = asset.Building == null ? UnknownCategory : asset.Building.Name
            })
            .Select(group => new DimensionCountProjection(
                group.Key.BuildingId,
                group.Key.Name,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        var countByLocation = await assets
            .GroupBy(asset => new
            {
                asset.LocationId,
                Name = asset.Location == null ? UnknownCategory : asset.Location.Name
            })
            .Select(group => new DimensionCountProjection(
                group.Key.LocationId,
                group.Key.Name,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        var countByAssetGroup = await assets
            .GroupBy(asset => new
            {
                asset.AssetGroupId,
                Name = asset.AssetGroup == null ? UnknownCategory : asset.AssetGroup.Name
            })
            .Select(group => new DimensionCountProjection(
                group.Key.AssetGroupId,
                group.Key.Name,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        var workOrders = ApplyWorkOrderDateFilters(
            _dbContext.WorkOrders.AsNoTracking().Where(item => item.IsInCanonicalSnapshot),
            query.WorkOrderDateFrom,
            query.WorkOrderDateTo);
        var assetIds = assets.Select(asset => asset.Id);
        var workOrderAssetIds = workOrders
            .Where(workOrder =>
                workOrder.AssetId.HasValue && assetIds.Contains(workOrder.AssetId.Value))
            .Select(workOrder => workOrder.AssetId!.Value)
            .Distinct();
        var assetsWithWorkOrders = await workOrderAssetIds.LongCountAsync(cancellationToken);

        var topAssets = await (
                from asset in assets
                join workOrder in workOrders.Where(item => item.AssetId.HasValue)
                    on asset.Id equals workOrder.AssetId!.Value
                group workOrder by new { asset.Id, asset.AssetCode, asset.Name }
                into grouped
                select new AssetWorkOrderCountProjection(
                    grouped.Key.Id,
                    grouped.Key.AssetCode,
                    grouped.Key.Name,
                    grouped.LongCount()))
            .ToListAsync(cancellationToken);

        return new AssetOverviewResponse(
            totalAssetCount,
            OrderDimensionCounts(countByBuilding),
            OrderDimensionCounts(countByLocation),
            OrderDimensionCounts(countByAssetGroup),
            assetsWithWorkOrders,
            totalAssetCount - assetsWithWorkOrders,
            topAssets
                .OrderByDescending(item => item.WorkOrderCount)
                .ThenBy(item => item.AssetCode, StringComparer.Ordinal)
                .Take(query.Top ?? 10)
                .Select(item => new AssetWorkOrderCountDto(
                    item.AssetId,
                    item.AssetCode,
                    item.AssetName,
                    item.WorkOrderCount))
                .ToArray(),
            KpiReliability.Yellow,
            new SnapshotAnalyticsMetadataDto(
                KpiReliability.Green,
                "core.Assets + core.WorkOrders",
                DateTimeOffset.UtcNow,
                totalAssetCount,
                [
                    "Only rows in the canonical WorkOrder snapshot are included.",
                    "Assets without work-order records must not be interpreted as healthy assets.",
                    "Top asset ranking has Yellow reliability because the distribution is highly skewed."
                ]));
    }

    public async Task<AssetMaintenanceActivityParetoResponse> GetMaintenanceActivityParetoAsync(
        AssetMaintenanceActivityParetoQuery query,
        CancellationToken cancellationToken = default)
    {
        var appliedTop = query.Top ?? 10;
        var workOrders = ApplyWorkOrderDateFilters(
            _dbContext.WorkOrders.AsNoTracking().Where(item => item.IsInCanonicalSnapshot),
            query.DateFrom,
            query.DateTo);
        var summary = await GetWorkOrderDateSummaryAsync(workOrders, cancellationToken);
        var assetsWithWorkOrders = await workOrders
            .Where(item => item.AssetId.HasValue)
            .Join(
                _dbContext.Assets.AsNoTracking(),
                workOrder => workOrder.AssetId!.Value,
                asset => asset.Id,
                (_, asset) => asset.Id)
            .Distinct()
            .LongCountAsync(cancellationToken);

        var topAssets = await (
                from workOrder in workOrders
                where workOrder.AssetId.HasValue
                join asset in _dbContext.Assets.AsNoTracking()
                    on workOrder.AssetId!.Value equals asset.Id
                group workOrder by new { asset.Id, asset.AssetCode, asset.Name }
                into grouped
                select new
                {
                    AssetId = grouped.Key.Id,
                    grouped.Key.AssetCode,
                    AssetName = grouped.Key.Name,
                    WorkOrderCount = grouped.LongCount()
                })
            .OrderByDescending(item => item.WorkOrderCount)
            .ThenBy(item => item.AssetCode)
            .ThenBy(item => item.AssetId)
            .Take(appliedTop)
            .ToListAsync(cancellationToken);

        var cumulativeCount = 0L;
        var paretoItems = topAssets
            .Select(item =>
            {
                cumulativeCount += item.WorkOrderCount;
                return new AssetMaintenanceActivityParetoItemDto(
                    item.AssetId,
                    item.AssetCode,
                    item.AssetName,
                    item.WorkOrderCount,
                    CalculatePercent(item.WorkOrderCount, summary.MatchedRecordCount, 4),
                    CalculatePercent(cumulativeCount, summary.MatchedRecordCount, 4));
            })
            .ToArray();

        return new AssetMaintenanceActivityParetoResponse(
            summary.MatchedRecordCount,
            assetsWithWorkOrders,
            appliedTop,
            paretoItems,
            new DateRangeMetadataDto(
                KpiReliability.Yellow,
                "core.WorkOrders + core.Assets",
                DateTimeOffset.UtcNow,
                query.DateFrom,
                query.DateTo,
                summary.ActualMinDate,
                summary.ActualMaxDate,
                nameof(WorkOrder.ReportedDateTime),
                summary.MatchedRecordCount,
                summary.ValidRecordCount,
                summary.MatchedRecordCount - summary.ValidRecordCount,
                TimeZoneAssumption,
                "asset-maintenance-activity-pareto/v1",
                [
                    "Counts represent canonical work-order record activity.",
                    "High activity does not indicate asset health, failure rate, or failure propensity."
                ]));
    }

    public async Task<InspectionPriorityResponse> GetInspectionPriorityAsync(
        InspectionPriorityQuery query,
        CancellationToken cancellationToken = default)
    {
        var appliedTop = query.Top ?? 10;
        var canonicalWorkOrders = _dbContext.WorkOrders
            .AsNoTracking()
            .Where(item => item.IsInCanonicalSnapshot && item.ReportedDateTime.HasValue);
        var latestSourceDateTime = query.AsOf.HasValue
            ? null
            : await canonicalWorkOrders.MaxAsync(
                item => (DateTime?)item.ReportedDateTime,
                cancellationToken);
        var asOf = query.AsOf
            ?? (latestSourceDateTime.HasValue
                ? DateOnly.FromDateTime(latestSourceDateTime.Value)
                : null);

        if (!asOf.HasValue)
        {
            return new InspectionPriorityResponse(
                CreateInspectionPriorityMetadata(null, null, 0, 0, 0, appliedTop),
                []);
        }

        var exclusiveEnd = asOf.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var last7Start = exclusiveEnd.AddDays(-7);
        var last30Start = exclusiveEnd.AddDays(-30);
        var previous30Start = exclusiveEnd.AddDays(-60);
        var last90Start = exclusiveEnd.AddDays(-90);
        var workOrdersThroughCutoff = canonicalWorkOrders
            .Where(item => item.ReportedDateTime < exclusiveEnd);

        var coverage = await workOrdersThroughCutoff
            .GroupBy(_ => 1)
            .Select(group => new InspectionPriorityCoverageProjection(
                group.LongCount(item => item.AssetId.HasValue),
                group.LongCount(item => !item.AssetId.HasValue)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new InspectionPriorityCoverageProjection(0, 0);

        var signalCounts = await workOrdersThroughCutoff
            .Where(workOrder => workOrder.AssetId.HasValue)
            .Where(workOrder =>
                workOrder.ReportedDateTime >= last90Start
                || workOrder.RawStatusCode == WorkOrderSourceState.Open)
            .GroupBy(workOrder => workOrder.AssetId!.Value)
            .Select(grouped => new InspectionPriorityCountProjection(
                    grouped.Key,
                    grouped.LongCount(item => item.ReportedDateTime >= last7Start),
                    grouped.LongCount(item => item.ReportedDateTime >= last30Start),
                    grouped.LongCount(item =>
                        item.ReportedDateTime >= previous30Start
                        && item.ReportedDateTime < last30Start),
                    grouped.LongCount(item => item.ReportedDateTime >= last90Start),
                    grouped.LongCount(item =>
                        item.RawStatusCode == WorkOrderSourceState.Open)))
            .ToListAsync(cancellationToken);
        var signalAssetIds = signalCounts.Select(item => item.AssetId).ToArray();
        var signalAssets = await _dbContext.Assets
            .AsNoTracking()
            .Where(asset => signalAssetIds.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.AssetCode, asset.Name })
            .ToDictionaryAsync(asset => asset.Id, cancellationToken);
        var signalRows = signalCounts
            .Where(item => signalAssets.ContainsKey(item.AssetId))
            .Select(item =>
            {
                var asset = signalAssets[item.AssetId];
                return new InspectionPrioritySignalProjection(
                    asset.Id,
                    asset.AssetCode,
                    asset.Name,
                    item.Last7Count,
                    item.Last30Count,
                    item.Previous30Count,
                    item.Last90Count,
                    item.OpenCount);
            })
            .ToArray();

        var rankedItems = signalRows
            .Select(item =>
            {
                var result = InspectionPriorityScoring.Calculate(new InspectionPrioritySignals(
                    item.Last7Count,
                    item.Last30Count,
                    item.Previous30Count,
                    item.Last90Count,
                    item.OpenCount));
                return new InspectionPriorityItemDto(
                    item.AssetId,
                    item.AssetCode,
                    item.AssetName,
                    result.Score,
                    result.Level,
                    item.Last7Count,
                    item.Last30Count,
                    item.Previous30Count,
                    item.Last90Count,
                    item.OpenCount,
                    result.ActivityChange,
                    result.Reasons);
            })
            .OrderByDescending(item => item.PriorityScore)
            .ThenByDescending(item => item.OpenCount)
            .ThenByDescending(item => item.Last30Count)
            .ThenBy(item => item.AssetCode, StringComparer.Ordinal)
            .ThenBy(item => item.AssetId)
            .Take(appliedTop)
            .ToArray();

        var analysisWindow = new InspectionPriorityAnalysisWindowDto(
            asOf.Value.AddDays(-6),
            asOf.Value.AddDays(-29),
            asOf.Value.AddDays(-59),
            asOf.Value.AddDays(-30),
            asOf.Value.AddDays(-89),
            asOf.Value);

        return new InspectionPriorityResponse(
            CreateInspectionPriorityMetadata(
                asOf,
                analysisWindow,
                coverage.EligibleWorkOrders,
                coverage.ExcludedUnlinkedWorkOrders,
                signalRows.LongLength,
                appliedTop),
            rankedItems);
    }

    public async Task<EarlyWarningResponse> GetEarlyWarningAsync(
        EarlyWarningQuery query,
        CancellationToken cancellationToken = default)
    {
        var appliedTop = query.Top ?? 10;
        var canonicalWorkOrders = _dbContext.WorkOrders
            .AsNoTracking()
            .Where(item => item.IsInCanonicalSnapshot && item.ReportedDateTime.HasValue);
        var latestSourceDateTime = query.AsOf.HasValue
            ? null
            : await canonicalWorkOrders.MaxAsync(
                item => (DateTime?)item.ReportedDateTime,
                cancellationToken);
        var asOf = query.AsOf
            ?? (latestSourceDateTime.HasValue
                ? DateOnly.FromDateTime(latestSourceDateTime.Value)
                : null);

        if (!asOf.HasValue)
        {
            return new EarlyWarningResponse(
                CreateEarlyWarningMetadata(null, null, 0, 0, 0, 0, 0, appliedTop),
                []);
        }

        var exclusiveEnd = asOf.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var last7Start = exclusiveEnd.AddDays(-7);
        var previous7Start = exclusiveEnd.AddDays(-14);
        var last30Start = exclusiveEnd.AddDays(-30);
        var previous30Start = exclusiveEnd.AddDays(-60);
        var last90Start = exclusiveEnd.AddDays(-90);
        var previous90Start = exclusiveEnd.AddDays(-180);
        var baselineEnd = new DateOnly(last30Start.Year, last30Start.Month, 1);
        var baselineStart = baselineEnd.AddMonths(-EarlyWarningScoring.BaselineMonthCount);
        var baselineStartDateTime = baselineStart.ToDateTime(TimeOnly.MinValue);
        var baselineEndDateTime = baselineEnd.ToDateTime(TimeOnly.MinValue);
        var workOrdersThroughCutoff = canonicalWorkOrders
            .Where(item => item.ReportedDateTime < exclusiveEnd);

        var coverage = await workOrdersThroughCutoff
            .GroupBy(_ => 1)
            .Select(group => new InspectionPriorityCoverageProjection(
                group.LongCount(item => item.AssetId.HasValue),
                group.LongCount(item => !item.AssetId.HasValue)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new InspectionPriorityCoverageProjection(0, 0);

        var signalCounts = await workOrdersThroughCutoff
            .Where(workOrder => workOrder.AssetId.HasValue)
            .GroupBy(workOrder => workOrder.AssetId!.Value)
            .Select(grouped => new EarlyWarningCountProjection(
                grouped.Key,
                grouped.LongCount(item => item.ReportedDateTime >= last7Start),
                grouped.LongCount(item =>
                    item.ReportedDateTime >= previous7Start
                    && item.ReportedDateTime < last7Start),
                grouped.LongCount(item => item.ReportedDateTime >= last30Start),
                grouped.LongCount(item =>
                    item.ReportedDateTime >= previous30Start
                    && item.ReportedDateTime < last30Start),
                grouped.LongCount(item => item.ReportedDateTime >= last90Start),
                grouped.LongCount(item =>
                    item.ReportedDateTime >= previous90Start
                    && item.ReportedDateTime < last90Start),
                grouped.LongCount(item => item.RawStatusCode == WorkOrderSourceState.Open),
                grouped.LongCount(item =>
                    item.RawStatusCode == WorkOrderSourceState.Open
                    && item.ReportedDateTime >= baselineStartDateTime
                    && item.ReportedDateTime < baselineEndDateTime)))
            .ToListAsync(cancellationToken);

        var baselineMonthlyCounts = await workOrdersThroughCutoff
            .Where(workOrder => workOrder.AssetId.HasValue)
            .Where(workOrder =>
                workOrder.ReportedDateTime >= baselineStartDateTime
                && workOrder.ReportedDateTime < baselineEndDateTime)
            .GroupBy(workOrder => new
            {
                AssetId = workOrder.AssetId!.Value,
                Year = workOrder.ReportedDateTime!.Value.Year,
                Month = workOrder.ReportedDateTime.Value.Month
            })
            .Select(grouped => new EarlyWarningMonthlyCountProjection(
                grouped.Key.AssetId,
                grouped.Key.Year,
                grouped.Key.Month,
                grouped.LongCount()))
            .ToListAsync(cancellationToken);

        var linkedAssetIds = workOrdersThroughCutoff
            .Where(workOrder => workOrder.AssetId.HasValue)
            .Select(workOrder => workOrder.AssetId!.Value)
            .Distinct();
        var assets = await _dbContext.Assets
            .AsNoTracking()
            .Where(asset => linkedAssetIds.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.AssetCode, asset.Name })
            .ToDictionaryAsync(asset => asset.Id, cancellationToken);
        var monthlyCountsByAsset = baselineMonthlyCounts
            .GroupBy(item => item.AssetId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var warningRows = signalCounts
            .Where(item => assets.ContainsKey(item.AssetId))
            .Select(item =>
            {
                var asset = assets[item.AssetId];
                var monthlyCounts = new long[EarlyWarningScoring.BaselineMonthCount];
                if (monthlyCountsByAsset.TryGetValue(item.AssetId, out var baselineCounts))
                {
                    foreach (var count in baselineCounts)
                    {
                        var monthIndex = ((count.Year - baselineStart.Year) * 12)
                            + count.Month
                            - baselineStart.Month;
                        if (monthIndex >= 0 && monthIndex < monthlyCounts.Length)
                        {
                            monthlyCounts[monthIndex] = count.Count;
                        }
                    }
                }

                var activeMonths = monthlyCounts.Count(count => count > 0);
                if (activeMonths < EarlyWarningScoring.MinimumActiveMonths)
                {
                    return new EarlyWarningItemDto(
                        asset.Id,
                        asset.AssetCode,
                        asset.Name,
                        null,
                        null,
                        EarlyWarningBaselineStatus.InsufficientBaseline,
                        item.Last7Count,
                        item.Previous7Count,
                        item.Last30Count,
                        item.Previous30Count,
                        item.Last90Count,
                        item.Previous90Count,
                        null,
                        null,
                        activeMonths,
                        null,
                        item.OpenCount,
                        [$"12 aylık baseline içinde en az 6 aktif ay gerekir; {activeMonths} aktif ay bulundu"]);
                }

                var baselineMedian = EarlyWarningScoring.Median(monthlyCounts);
                var baselineMad = EarlyWarningScoring.MedianAbsoluteDeviation(
                    monthlyCounts,
                    baselineMedian);
                var result = EarlyWarningScoring.Calculate(new EarlyWarningSignals(
                    item.Last7Count,
                    item.Previous7Count,
                    item.Last30Count,
                    item.Previous30Count,
                    item.Last90Count,
                    item.OpenCount,
                    item.BaselineOpenCount,
                    baselineMedian,
                    baselineMad));

                return new EarlyWarningItemDto(
                    asset.Id,
                    asset.AssetCode,
                    asset.Name,
                    result.Score,
                    result.Level,
                    EarlyWarningBaselineStatus.Sufficient,
                    item.Last7Count,
                    item.Previous7Count,
                    item.Last30Count,
                    item.Previous30Count,
                    item.Last90Count,
                    item.Previous90Count,
                    baselineMedian,
                    baselineMad,
                    activeMonths,
                    result.Deviation,
                    item.OpenCount,
                    result.Reasons);
            })
            .ToArray();

        var eligibleAssets = warningRows.LongCount(item =>
            item.BaselineStatus == EarlyWarningBaselineStatus.Sufficient);
        var insufficientAssets = warningRows.LongLength - eligibleAssets;
        var rankedItems = warningRows
            .OrderBy(item => item.BaselineStatus == EarlyWarningBaselineStatus.Sufficient ? 0 : 1)
            .ThenByDescending(item => item.WarningScore)
            .ThenByDescending(item => item.Last30Count)
            .ThenByDescending(item => item.Last7Count)
            .ThenBy(item => item.AssetCode, StringComparer.Ordinal)
            .ThenBy(item => item.AssetId)
            .Take(appliedTop)
            .ToArray();
        var baselineWindow = new EarlyWarningBaselineWindowDto(
            baselineStart,
            baselineEnd.AddDays(-1),
            EarlyWarningScoring.BaselineMonthCount,
            EarlyWarningScoring.MinimumActiveMonths);

        return new EarlyWarningResponse(
            CreateEarlyWarningMetadata(
                asOf,
                baselineWindow,
                warningRows.LongLength,
                eligibleAssets,
                insufficientAssets,
                coverage.EligibleWorkOrders,
                coverage.ExcludedUnlinkedWorkOrders,
                appliedTop),
            rankedItems);
    }

    public async Task<WorkOrderOverviewResponse> GetOverviewAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var workOrders = ApplyWorkOrderFilters(_dbContext.WorkOrders.AsNoTracking(), query);
        var summary = await GetWorkOrderDateSummaryAsync(workOrders, cancellationToken);
        var openWorkOrders = await workOrders.LongCountAsync(
            item => item.RawStatusCode == WorkOrderSourceState.Open,
            cancellationToken);
        var closedWorkOrders = await workOrders.LongCountAsync(
            item => item.RawStatusCode == WorkOrderSourceState.Closed,
            cancellationToken);
        var last30DaysStart = DateTime.Today.AddDays(-29);
        var last30DaysWorkOrders = await workOrders.LongCountAsync(
            item => item.ReportedDateTime >= last30DaysStart,
            cancellationToken);

        var byDiscipline = await GetCategoryCountsAsync(
            workOrders.Select(item => item.Discipline),
            cancellationToken);
        var byWorkType = await GetCategoryCountsAsync(
            workOrders.Select(item => item.WorkType),
            cancellationToken);
        var byStatus = await GetCategoryCountsAsync(
            workOrders.Select(item => item.Status),
            cancellationToken);
        var byRawStatusCode = await GetCategoryCountsAsync(
            workOrders.Select(item => item.RawStatusCode),
            cancellationToken);
        var byFailureType = await GetCategoryCountsAsync(
            workOrders.Select(item => item.FailureType),
            cancellationToken);

        var byBuilding = await workOrders
            .GroupBy(item => new
            {
                item.BuildingId,
                Name = item.Building == null ? UnknownCategory : item.Building.Name
            })
            .Select(group => new DimensionCountProjection(
                group.Key.BuildingId,
                group.Key.Name,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        var byLocation = await workOrders
            .GroupBy(item => new
            {
                item.LocationId,
                Name = item.Location == null ? UnknownCategory : item.Location.Name
            })
            .Select(group => new DimensionCountProjection(
                group.Key.LocationId,
                group.Key.Name,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        return new WorkOrderOverviewResponse(
            summary.MatchedRecordCount,
            openWorkOrders,
            closedWorkOrders,
            summary.MatchedRecordCount - openWorkOrders - closedWorkOrders,
            last30DaysWorkOrders,
            byDiscipline,
            byWorkType,
            byRawStatusCode,
            byStatus,
            byFailureType,
            OrderDimensionCounts(byBuilding),
            OrderDimensionCounts(byLocation),
            KpiReliability.Yellow,
            KpiReliability.Yellow,
            CreateWorkOrderMetadata(query, summary, KpiReliability.Green));
    }

    public async Task<WorkOrderTrendResponse> GetTrendAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureMonthlyGrain(query.Grain ?? TimeGrain.Month);

        var workOrders = ApplyWorkOrderFilters(_dbContext.WorkOrders.AsNoTracking(), query);
        var summary = await GetWorkOrderDateSummaryAsync(workOrders, cancellationToken);
        var groupedPoints = await workOrders
            .Where(item => item.ReportedDateTime.HasValue)
            .GroupBy(item => new
            {
                Year = item.ReportedDateTime!.Value.Year,
                Month = item.ReportedDateTime.Value.Month
            })
            .Select(group => new TrendPointProjection(
                group.Key.Year,
                group.Key.Month,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        return new WorkOrderTrendResponse(
            TimeGrain.Month,
            groupedPoints
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month)
                .Select(item => new TrendPointDto(
                    new DateOnly(item.Year, item.Month, 1),
                    item.Count))
                .ToArray(),
            CreateWorkOrderMetadata(query, summary, KpiReliability.Green));
    }

    public async Task<WorkOrderActivityResponse> GetActivityAsync(
        WorkOrderActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        var workOrders = ApplyWorkOrderActivityFilters(
            _dbContext.WorkOrders.AsNoTracking().Where(item => item.IsInCanonicalSnapshot),
            query);
        var summary = await GetWorkOrderDateSummaryAsync(workOrders, cancellationToken);
        var groupedPoints = await workOrders
            .Where(item => item.ReportedDateTime.HasValue)
            .GroupBy(item => new
            {
                Year = item.ReportedDateTime!.Value.Year,
                Month = item.ReportedDateTime.Value.Month
            })
            .Select(group => new TrendPointProjection(
                group.Key.Year,
                group.Key.Month,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var byDiscipline = await GetCategoryCountsAsync(
            workOrders.Select(item => item.Discipline),
            cancellationToken);

        return new WorkOrderActivityResponse(
            TimeGrain.Month,
            groupedPoints
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month)
                .Select(item => new TrendPointDto(
                    new DateOnly(item.Year, item.Month, 1),
                    item.Count))
                .ToArray(),
            byDiscipline,
            NormalizeOptionalFilter(query.Discipline),
            new DateRangeMetadataDto(
                KpiReliability.Green,
                "core.WorkOrders",
                DateTimeOffset.UtcNow,
                query.DateFrom,
                query.DateTo,
                summary.ActualMinDate,
                summary.ActualMaxDate,
                nameof(WorkOrder.ReportedDateTime),
                summary.MatchedRecordCount,
                summary.ValidRecordCount,
                summary.MatchedRecordCount - summary.ValidRecordCount,
                TimeZoneAssumption,
                "canonical-work-order-activity/v2",
                [
                    "Only rows in the canonical WorkOrder snapshot are included.",
                    "Discipline values are exact raw source taxonomy.",
                    "The legacy HistoricalWorkOrders snapshot is not queried."
                ]));
    }

    public async Task<ScadaOverviewResponse> GetOverviewAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sourceDateExclusive = DateTime.Today.AddDays(1);
        var alarms = ApplyScadaFilters(_dbContext.ScadaAlarmEvents.AsNoTracking(), query);
        var matchedRecordCount = await alarms.LongCountAsync(cancellationToken);
        var validReceived = WhereValidScadaReceivedAt(alarms, sourceDateExclusive);
        var validRecordCount = await validReceived.LongCountAsync(cancellationToken);
        var actualMinDate = await validReceived
            .MinAsync(item => item.ReceivedAt, cancellationToken);
        var actualMaxDate = await validReceived
            .MaxAsync(item => item.ReceivedAt, cancellationToken);

        var invalidOrMissingTimestampCount = await alarms.LongCountAsync(
            item => item.ReceivedAt == null
                || item.DateTimeParseStatus == null
                || item.DateTimeParseStatus.Contains("InvalidDate")
                || item.DateTimeParseStatus.Contains("InvalidTime")
                || item.DateTimeParseStatus.Contains("DateOnlySource")
                || item.DateTimeParseStatus.Contains("PlaceholderX")
                || item.DateTimeParseStatus.Contains("SuspiciousYear"),
            cancellationToken);
        var dateQualityIssueCount = await alarms.LongCountAsync(
            item => item.ReceivedAt == null
                || item.DateTimeParseStatus == null
                || item.DateTimeParseStatus.Contains("InvalidDate")
                || item.DateTimeParseStatus.Contains("InvalidTime")
                || item.DateTimeParseStatus.Contains("DateOnlySource")
                || item.DateTimeParseStatus.Contains("PlaceholderX")
                || item.DateTimeParseStatus.Contains("SuspiciousYear")
                || item.DateTimeParseStatus.Contains("FutureDate")
                || item.DateTimeParseStatus.Contains("ClearedBeforeReceived")
                || item.ReceivedAt >= sourceDateExclusive,
            cancellationToken);

        var bySourceSheet = await GetCategoryCountsAsync(
            alarms.Select(item => (string?)item.SourceSheet),
            cancellationToken);
        var byAlarmType = await GetCategoryCountsAsync(
            alarms.Select(item => item.AlarmType),
            cancellationToken);
        var byInterventionLevel = await GetCategoryCountsAsync(
            alarms.Select(item => item.InterventionLevel),
            cancellationToken);
        var bySection = await GetCategoryCountsAsync(
            alarms.Select(item => item.SectionRaw),
            cancellationToken);
        var byLocationRaw = await GetCategoryCountsAsync(
            alarms.Select(item => item.LocationRaw),
            cancellationToken);

        return new ScadaOverviewResponse(
            matchedRecordCount,
            bySourceSheet,
            byAlarmType,
            byInterventionLevel,
            bySection,
            byLocationRaw,
            invalidOrMissingTimestampCount,
            dateQualityIssueCount,
            KpiReliability.Yellow,
            KpiReliability.Yellow,
            CreateScadaMetadata(
                query,
                matchedRecordCount,
                validRecordCount,
                actualMinDate,
                actualMaxDate,
                KpiReliability.Green,
                [
                    "Counts represent source occurrences, not unique physical alarms.",
                    "When a date filter is provided, rows with NULL ReceivedAt are outside the matched set.",
                    "Section and location values are raw source taxonomy."
                ]));
    }

    public async Task<ScadaTrendResponse> GetTrendAsync(
        ScadaAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureMonthlyGrain(query.Grain ?? TimeGrain.Month);

        var sourceDateExclusive = DateTime.Today.AddDays(1);
        var alarms = ApplyScadaFilters(_dbContext.ScadaAlarmEvents.AsNoTracking(), query);
        var matchedRecordCount = await alarms.LongCountAsync(cancellationToken);
        var validReceived = WhereValidScadaReceivedAt(alarms, sourceDateExclusive);
        var validRecordCount = await validReceived.LongCountAsync(cancellationToken);
        var actualMinDate = await validReceived
            .MinAsync(item => item.ReceivedAt, cancellationToken);
        var actualMaxDate = await validReceived
            .MaxAsync(item => item.ReceivedAt, cancellationToken);

        var groupedPoints = await validReceived
            .GroupBy(item => new
            {
                Year = item.ReceivedAt!.Value.Year,
                Month = item.ReceivedAt.Value.Month
            })
            .Select(group => new TrendPointProjection(
                group.Key.Year,
                group.Key.Month,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        var excludedCount = matchedRecordCount - validRecordCount;

        return new ScadaTrendResponse(
            TimeGrain.Month,
            groupedPoints
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month)
                .Select(item => new TrendPointDto(
                    new DateOnly(item.Year, item.Month, 1),
                    item.Count))
                .ToArray(),
            new QualitySummaryDto(validRecordCount, excludedCount),
            CreateScadaMetadata(
                query,
                matchedRecordCount,
                validRecordCount,
                actualMinDate,
                actualMaxDate,
                KpiReliability.Yellow,
                [
                    "Only records with a trustworthy ReceivedAt participate in the trend.",
                    "Empty months are not synthesized."
                ]));
    }

    public async Task<ScadaClearanceIntervalResponse> GetClearanceIntervalAsync(
        ScadaClearanceIntervalQuery query,
        CancellationToken cancellationToken = default)
    {
        var dateFrom = query.DateFrom?.ToDateTime(TimeOnly.MinValue);
        var dateToExclusive = query.DateTo?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var sourceSheet = NormalizeOptionalFilter(query.SourceSheet);
        var alarmType = NormalizeOptionalFilter(query.AlarmType);
        var interventionLevel = NormalizeOptionalFilter(query.InterventionLevel);
        var section = NormalizeOptionalFilter(query.Section);
        var locationRaw = NormalizeOptionalFilter(query.LocationRaw);
        var sourceDateExclusive = DateTime.Today.AddDays(1);

        var statisticsRows = await _dbContext.Database
            .SqlQueryRaw<ScadaClearanceStatisticsProjection>(
                ScadaClearanceStatisticsSql,
                DateTimeParameter("@dateFrom", dateFrom),
                DateTimeParameter("@dateToExclusive", dateToExclusive),
                StringParameter("@sourceSheet", sourceSheet, 200),
                StringParameter("@alarmType", alarmType, 300),
                StringParameter("@interventionLevel", interventionLevel, 200),
                StringParameter("@section", section, 500),
                StringParameter("@locationRaw", locationRaw, 1000),
                DateTimeParameter("@sourceDateExclusive", sourceDateExclusive))
            .ToListAsync(cancellationToken);
        var statistics = statisticsRows.Single();
        var excludedOccurrences =
            statistics.TotalMatchedOccurrences - statistics.EligibleOccurrences;
        decimal? eligibilityPercent = statistics.TotalMatchedOccurrences == 0
            ? null
            : CalculatePercent(
                statistics.EligibleOccurrences,
                statistics.TotalMatchedOccurrences,
                2);

        return new ScadaClearanceIntervalResponse(
            statistics.TotalMatchedOccurrences,
            statistics.EligibleOccurrences,
            excludedOccurrences,
            eligibilityPercent,
            statistics.MedianMinutes,
            statistics.P90Minutes,
            new ScadaClearanceIntervalAppliedFiltersDto(
                sourceSheet,
                alarmType,
                interventionLevel,
                section,
                locationRaw),
            new DateRangeMetadataDto(
                KpiReliability.Yellow,
                "core.ScadaAlarmEvents",
                DateTimeOffset.UtcNow,
                query.DateFrom,
                query.DateTo,
                statistics.ActualMinDate,
                statistics.ActualMaxDate,
                nameof(ScadaAlarmEvent.ReceivedAt),
                statistics.TotalMatchedOccurrences,
                statistics.EligibleOccurrences,
                excludedOccurrences,
                TimeZoneAssumption,
                "scada-clearance-interval/v1",
                [
                    "Intervals represent eligible SCADA source occurrences, not unique physical alarms.",
                    "SCADA clearance interval is not MTTR or repair duration.",
                    "Median and p90 exclude records that fail timestamp quality rules."
                ]));
    }

    public async Task<ImportQualityOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var batches = _dbContext.ImportBatches.AsNoTracking();
        var sourceRecords = _dbContext.ImportSourceRecords.AsNoTracking();

        var totalBatches = await batches.LongCountAsync(cancellationToken);
        var batchesByStatus = await GetCategoryCountsAsync(
            batches.Select(item => (string?)item.Status),
            cancellationToken);
        var batchesBySourceType = await batches
            .GroupBy(item => item.SourceType)
            .Select(group => new SourceTypeCountProjection(
                group.Key,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var sourceRecordsByParseStatus = await GetCategoryCountsAsync(
            sourceRecords.Select(item => (string?)item.ParseStatus),
            cancellationToken);
        var importErrorCount = await _dbContext.ImportErrors
            .AsNoTracking()
            .LongCountAsync(cancellationToken);
        var errorsBySourceType = await _dbContext.ImportErrors
            .AsNoTracking()
            .GroupBy(item => item.ImportBatch.SourceType)
            .Select(group => new SourceTypeCountProjection(
                group.Key,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var fingerprintAlgorithmDistribution = await sourceRecords
            .GroupBy(item => item.FingerprintAlgorithm == null || item.FingerprintAlgorithm == string.Empty
                ? LegacyFingerprintAlgorithm
                : item.FingerprintAlgorithm)
            .Select(group => new CategoryCountProjection(
                group.Key,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        var legacySourceRecordCount = fingerprintAlgorithmDistribution
            .Where(item => item.Category == LegacyFingerprintAlgorithm)
            .Sum(item => item.Count);
        var totalSourceRecordCount = fingerprintAlgorithmDistribution.Sum(item => item.Count);

        return new ImportQualityOverviewResponse(
            totalBatches,
            batchesByStatus,
            batchesBySourceType
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.SourceType, StringComparer.Ordinal)
                .Select(item => new SourceTypeBatchCountDto(item.SourceType, item.Count))
                .ToArray(),
            sourceRecordsByParseStatus,
            importErrorCount,
            errorsBySourceType
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.SourceType, StringComparer.Ordinal)
                .Select(item => new SourceTypeBatchCountDto(item.SourceType, item.Count))
                .ToArray(),
            fingerprintAlgorithmDistribution
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Category, StringComparer.Ordinal)
                .Select(item => new CategoryCountDto(item.Category, item.Count))
                .ToArray(),
            legacySourceRecordCount,
            totalSourceRecordCount - legacySourceRecordCount,
            new SnapshotAnalyticsMetadataDto(
                KpiReliability.Green,
                "ingestion.ImportBatches + ingestion.ImportSourceRecords + ingestion.ImportErrors",
                DateTimeOffset.UtcNow,
                totalSourceRecordCount,
                ["Failed and InProgress batches are retained as audit history."]));
    }

    private static IQueryable<Asset> ApplyAssetFilters(
        IQueryable<Asset> query,
        AssetOverviewQuery filters)
    {
        if (filters.BuildingId.HasValue)
        {
            query = query.Where(item => item.BuildingId == filters.BuildingId);
        }

        if (filters.LocationId.HasValue)
        {
            query = query.Where(item => item.LocationId == filters.LocationId);
        }

        if (filters.AssetGroupId.HasValue)
        {
            query = query.Where(item => item.AssetGroupId == filters.AssetGroupId);
        }

        if (filters.AssetId.HasValue)
        {
            query = query.Where(item => item.Id == filters.AssetId);
        }

        return query;
    }

    private static IQueryable<WorkOrder> ApplyWorkOrderFilters(
        IQueryable<WorkOrder> query,
        WorkOrderAnalyticsQuery filters)
    {
        query = query.Where(item => item.IsInCanonicalSnapshot);
        query = ApplyWorkOrderDateFilters(query, filters.DateFrom, filters.DateTo);

        if (!string.IsNullOrWhiteSpace(filters.Discipline))
        {
            query = query.Where(item => item.Discipline == filters.Discipline);
        }

        if (!string.IsNullOrWhiteSpace(filters.WorkType))
        {
            query = query.Where(item => item.WorkType == filters.WorkType);
        }

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            query = query.Where(item => item.Status == filters.Status);
        }

        if (!string.IsNullOrWhiteSpace(filters.FailureType))
        {
            query = query.Where(item => item.FailureType == filters.FailureType);
        }

        if (filters.BuildingId.HasValue)
        {
            query = query.Where(item => item.BuildingId == filters.BuildingId);
        }

        if (filters.LocationId.HasValue)
        {
            query = query.Where(item => item.LocationId == filters.LocationId);
        }

        if (filters.AssetId.HasValue)
        {
            query = query.Where(item => item.AssetId == filters.AssetId);
        }

        return query;
    }

    private static IQueryable<WorkOrder> ApplyWorkOrderDateFilters(
        IQueryable<WorkOrder> query,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        if (dateFrom.HasValue)
        {
            var inclusiveStart = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReportedDateTime >= inclusiveStart);
        }

        if (dateTo.HasValue)
        {
            var exclusiveEnd = dateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReportedDateTime < exclusiveEnd);
        }

        return query;
    }

    private static IQueryable<WorkOrder> ApplyWorkOrderActivityFilters(
        IQueryable<WorkOrder> query,
        WorkOrderActivityQuery filters)
    {
        if (filters.DateFrom.HasValue)
        {
            var inclusiveStart = filters.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReportedDateTime >= inclusiveStart);
        }

        if (filters.DateTo.HasValue)
        {
            var exclusiveEnd = filters.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReportedDateTime < exclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(filters.Discipline))
        {
            query = query.Where(item => item.Discipline == filters.Discipline);
        }

        return query;
    }

    private static IQueryable<ScadaAlarmEvent> ApplyScadaFilters(
        IQueryable<ScadaAlarmEvent> query,
        ScadaAnalyticsQuery filters)
    {
        if (filters.DateFrom.HasValue)
        {
            var inclusiveStart = filters.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReceivedAt >= inclusiveStart);
        }

        if (filters.DateTo.HasValue)
        {
            var exclusiveEnd = filters.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.ReceivedAt < exclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(filters.SourceSheet))
        {
            query = query.Where(item => item.SourceSheet == filters.SourceSheet);
        }

        if (!string.IsNullOrWhiteSpace(filters.AlarmType))
        {
            query = query.Where(item => item.AlarmType == filters.AlarmType);
        }

        if (!string.IsNullOrWhiteSpace(filters.InterventionLevel))
        {
            query = query.Where(item => item.InterventionLevel == filters.InterventionLevel);
        }

        if (!string.IsNullOrWhiteSpace(filters.Section))
        {
            query = query.Where(item => item.SectionRaw == filters.Section);
        }

        if (!string.IsNullOrWhiteSpace(filters.LocationRaw))
        {
            query = query.Where(item => item.LocationRaw == filters.LocationRaw);
        }

        return query;
    }

    private static IQueryable<ScadaAlarmEvent> WhereValidScadaReceivedAt(
        IQueryable<ScadaAlarmEvent> query,
        DateTime sourceDateExclusive) =>
        query.Where(item =>
            item.ReceivedAt.HasValue
            && item.ReceivedAt < sourceDateExclusive
            && item.DateTimeParseStatus != null
            && item.DateTimeParseStatus.Contains("Received:Parsed")
            && !item.DateTimeParseStatus.Contains("Received:InvalidDate")
            && !item.DateTimeParseStatus.Contains("Received:InvalidTime")
            && !item.DateTimeParseStatus.Contains("Received:DateOnlySource")
            && !item.DateTimeParseStatus.Contains("Received:PlaceholderX")
            && !item.DateTimeParseStatus.Contains("Received:SuspiciousYear"));

    private static async Task<IReadOnlyList<CategoryCountDto>> GetCategoryCountsAsync(
        IQueryable<string?> values,
        CancellationToken cancellationToken)
    {
        var counts = await values
            .GroupBy(value => value)
            .Select(group => new CategoryCountProjection(
                group.Key ?? UnknownCategory,
                group.LongCount()))
            .ToListAsync(cancellationToken);

        return counts
            .GroupBy(item => string.IsNullOrEmpty(item.Category)
                ? UnknownCategory
                : item.Category)
            .Select(group => new CategoryCountDto(
                group.Key,
                group.Sum(item => item.Count)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<DatedSummaryProjection> GetWorkOrderDateSummaryAsync(
        IQueryable<WorkOrder> workOrders,
        CancellationToken cancellationToken) =>
        await workOrders
            .GroupBy(_ => 1)
            .Select(group => new DatedSummaryProjection(
                group.LongCount(),
                group.LongCount(item => item.ReportedDateTime.HasValue),
                group.Min(item => item.ReportedDateTime),
                group.Max(item => item.ReportedDateTime)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? new DatedSummaryProjection(0, 0, null, null);

    private static decimal CalculatePercent(long numerator, long denominator, int decimals) =>
        denominator == 0
            ? 0m
            : Math.Round(
                numerator * 100m / denominator,
                decimals,
                MidpointRounding.AwayFromZero);

    private static InspectionPriorityMetadataDto CreateInspectionPriorityMetadata(
        DateOnly? asOf,
        InspectionPriorityAnalysisWindowDto? analysisWindow,
        long eligibleWorkOrders,
        long excludedUnlinkedWorkOrders,
        long totalAssetsEvaluated,
        int appliedTop)
    {
        var coverageDenominator = eligibleWorkOrders + excludedUnlinkedWorkOrders;
        return new InspectionPriorityMetadataDto(
            asOf,
            analysisWindow,
            eligibleWorkOrders,
            excludedUnlinkedWorkOrders,
            CalculatePercent(eligibleWorkOrders, coverageDenominator, 4),
            totalAssetsEvaluated,
            appliedTop,
            "core.WorkOrders + core.Assets",
            InspectionPriorityScoring.Version,
            [
                "Inspection Priority is explainable WorkOrder-activity decision support; it is not failure probability or an asset health score.",
                "Only canonical WorkOrders with a resolved AssetId and ReportedDateTime on or before AsOf contribute to asset scores.",
                "Unlinked WorkOrders are disclosed in coverage metadata and are not mapped through raw asset codes.",
                "Assets are evaluated when they have activity in the last 90 days or an open source-state WorkOrder.",
                "The legacy HistoricalWorkOrders snapshot and SCADA records are not queried."
            ]);
    }

    private static EarlyWarningMetadataDto CreateEarlyWarningMetadata(
        DateOnly? asOf,
        EarlyWarningBaselineWindowDto? baselineWindow,
        long totalAssetsConsidered,
        long eligibleAssets,
        long insufficientBaselineAssets,
        long eligibleWorkOrders,
        long excludedUnlinkedWorkOrders,
        int appliedTop)
    {
        var coverageDenominator = eligibleWorkOrders + excludedUnlinkedWorkOrders;
        return new EarlyWarningMetadataDto(
            asOf,
            baselineWindow,
            totalAssetsConsidered,
            eligibleAssets,
            insufficientBaselineAssets,
            eligibleWorkOrders,
            excludedUnlinkedWorkOrders,
            CalculatePercent(eligibleWorkOrders, coverageDenominator, 4),
            appliedTop,
            "core.WorkOrders + core.Assets",
            EarlyWarningScoring.Version,
            [
                "Early Warning identifies explainable deviation from an asset's own WorkOrder activity history; it is not failure probability or an asset health score.",
                "The baseline uses 12 complete calendar months before the month containing the current 30-day window; the current window is excluded.",
                "At least 6 active baseline months are required; assets below that threshold are marked INSUFFICIENT_BASELINE and are not scored.",
                "Only canonical WorkOrders with a resolved AssetId and ReportedDateTime on or before AsOf are used.",
                "Unlinked WorkOrders are disclosed but not mapped through raw asset codes.",
                "SCADA and the legacy HistoricalWorkOrders snapshot are not queried."
            ]);
    }

    private static string? NormalizeOptionalFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static SqlParameter DateTimeParameter(string name, DateTime? value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.HasValue ? value.Value : DBNull.Value
        };

    private static SqlParameter StringParameter(string name, string? value, int size) =>
        new(name, SqlDbType.NVarChar, size)
        {
            Value = value is null ? DBNull.Value : value
        };

    private static DateRangeMetadataDto CreateWorkOrderMetadata(
        WorkOrderAnalyticsQuery query,
        DatedSummaryProjection summary,
        KpiReliability reliability) =>
        new(
            reliability,
            "core.WorkOrders",
            DateTimeOffset.UtcNow,
            query.DateFrom,
            query.DateTo,
            summary.ActualMinDate,
            summary.ActualMaxDate,
            nameof(WorkOrder.ReportedDateTime),
            summary.MatchedRecordCount,
            summary.ValidRecordCount,
            summary.MatchedRecordCount - summary.ValidRecordCount,
            TimeZoneAssumption,
            "work-order-analytics/v2",
            [
                "Only rows in the canonical WorkOrder snapshot are included.",
                "Open/closed semantics use RawStatusCode A/K; workflow Status remains separate.",
                "The legacy HistoricalWorkOrders snapshot is not queried."
            ]);

    private static DateRangeMetadataDto CreateScadaMetadata(
        ScadaAnalyticsQuery query,
        long matchedRecordCount,
        long validRecordCount,
        DateTime? actualMinDate,
        DateTime? actualMaxDate,
        KpiReliability reliability,
        IReadOnlyList<string> notes) =>
        new(
            reliability,
            "core.ScadaAlarmEvents",
            DateTimeOffset.UtcNow,
            query.DateFrom,
            query.DateTo,
            actualMinDate,
            actualMaxDate,
            nameof(ScadaAlarmEvent.ReceivedAt),
            matchedRecordCount,
            validRecordCount,
            matchedRecordCount - validRecordCount,
            TimeZoneAssumption,
            "scada-received-at-quality/v1",
            notes);

    private static DimensionCountDto MapDimensionCount(DimensionCountProjection item) =>
        new(item.Id, item.Name, item.Count);

    private static IReadOnlyList<DimensionCountDto> OrderDimensionCounts(
        IEnumerable<DimensionCountProjection> items) =>
        items
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Select(MapDimensionCount)
            .ToArray();

    private static void EnsureMonthlyGrain(TimeGrain grain)
    {
        if (grain != TimeGrain.Month)
        {
            throw new ArgumentOutOfRangeException(nameof(grain), grain, "Only Month is supported.");
        }
    }

    private sealed record CategoryCountProjection(string Category, long Count);

    private sealed record DimensionCountProjection(long? Id, string Name, long Count);

    private sealed record AssetWorkOrderCountProjection(
        long AssetId,
        string AssetCode,
        string AssetName,
        long WorkOrderCount);

    private sealed record TrendPointProjection(int Year, int Month, long Count);

    private sealed record SourceTypeCountProjection(string SourceType, long Count);

    private sealed record DatedSummaryProjection(
        long MatchedRecordCount,
        long ValidRecordCount,
        DateTime? ActualMinDate,
        DateTime? ActualMaxDate);

    private sealed record InspectionPriorityCoverageProjection(
        long EligibleWorkOrders,
        long ExcludedUnlinkedWorkOrders);

    private sealed record InspectionPrioritySignalProjection(
        long AssetId,
        string AssetCode,
        string AssetName,
        long Last7Count,
        long Last30Count,
        long Previous30Count,
        long Last90Count,
        long OpenCount);

    private sealed record InspectionPriorityCountProjection(
        long AssetId,
        long Last7Count,
        long Last30Count,
        long Previous30Count,
        long Last90Count,
        long OpenCount);

    private sealed record EarlyWarningCountProjection(
        long AssetId,
        long Last7Count,
        long Previous7Count,
        long Last30Count,
        long Previous30Count,
        long Last90Count,
        long Previous90Count,
        long OpenCount,
        long BaselineOpenCount);

    private sealed record EarlyWarningMonthlyCountProjection(
        long AssetId,
        int Year,
        int Month,
        long Count);

    private sealed class ScadaClearanceStatisticsProjection
    {
        public long TotalMatchedOccurrences { get; init; }
        public long EligibleOccurrences { get; init; }
        public DateTime? ActualMinDate { get; init; }
        public DateTime? ActualMaxDate { get; init; }
        public decimal? MedianMinutes { get; init; }
        public decimal? P90Minutes { get; init; }
    }
}
