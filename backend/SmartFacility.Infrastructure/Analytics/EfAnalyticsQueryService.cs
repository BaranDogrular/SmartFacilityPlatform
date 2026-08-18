using Microsoft.EntityFrameworkCore;
using SmartFacility.Application.Analytics.Abstractions;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Analytics;

public sealed class EfAnalyticsQueryService(SmartFacilityDbContext dbContext) :
    IAssetAnalyticsService,
    IWorkOrderAnalyticsService,
    IScadaAnalyticsService,
    IImportQualityAnalyticsService
{
    private const string UnknownCategory = "<Unknown>";
    private const string LegacyFingerprintAlgorithm = "<Legacy>";
    private const string TimeZoneAssumption = "UnspecifiedSourceLocal";

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
            _dbContext.WorkOrders.AsNoTracking(),
            query.WorkOrderDateFrom,
            query.WorkOrderDateTo);
        var assetIds = assets.Select(asset => asset.Id);
        var workOrderAssetIds = workOrders
            .Where(workOrder =>
                workOrder.AssetId.HasValue && assetIds.Contains(workOrder.AssetId.Value))
            .Select(workOrder => workOrder.AssetId!.Value)
            .Distinct();
        var assetsWithCurrentWorkOrders = await workOrderAssetIds.LongCountAsync(cancellationToken);

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
            assetsWithCurrentWorkOrders,
            totalAssetCount - assetsWithCurrentWorkOrders,
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
                    "HistoricalWorkOrders are excluded.",
                    "Assets without current work orders must not be interpreted as healthy assets.",
                    "Top asset ranking has Yellow reliability because the distribution is highly skewed."
                ]));
    }

    public async Task<WorkOrderOverviewResponse> GetOverviewAsync(
        WorkOrderAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var workOrders = ApplyWorkOrderFilters(_dbContext.WorkOrders.AsNoTracking(), query);
        var summary = await GetWorkOrderDateSummaryAsync(workOrders, cancellationToken);

        var byDiscipline = await GetCategoryCountsAsync(
            workOrders.Select(item => item.Discipline),
            cancellationToken);
        var byWorkType = await GetCategoryCountsAsync(
            workOrders.Select(item => item.WorkType),
            cancellationToken);
        var byStatus = await GetCategoryCountsAsync(
            workOrders.Select(item => item.Status),
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
            byDiscipline,
            byWorkType,
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
            "work-order-analytics/v1",
            [
                "HistoricalWorkOrders are excluded.",
                "Raw taxonomy values are not normalized."
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
}
