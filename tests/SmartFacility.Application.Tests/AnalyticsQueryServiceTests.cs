using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class AnalyticsQueryServiceTests
{
    [Fact]
    public async Task Asset_overview_aggregates_dimensions_and_current_work_order_presence()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, group, firstAsset, secondAsset) =
            await SeedAssetDimensionsAsync(database);

        database.Context.WorkOrders.AddRange(
            WorkOrder("WO-1", new DateTime(2026, 1, 2, 23, 59, 59), firstAsset, building, location),
            WorkOrder("WO-2", new DateTime(2026, 1, 3, 0, 0, 0), secondAsset, building, location));
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "WO-HISTORICAL",
            ReportedDateTime = new DateTime(2026, 1, 2, 12, 0, 0),
            Description = "Historical row",
            Discipline = "Electrical",
            LocationNameRaw = location.Name,
            ResolutionDurationRaw = "10",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetOverviewAsync(new AssetOverviewQuery
        {
            WorkOrderDateFrom = new DateOnly(2026, 1, 2),
            WorkOrderDateTo = new DateOnly(2026, 1, 2)
        });

        Assert.Equal(2, response.TotalAssetCount);
        Assert.Equal(2, Assert.Single(response.CountByBuilding).Count);
        Assert.Equal(2, Assert.Single(response.CountByLocation).Count);
        Assert.Equal(2, Assert.Single(response.CountByAssetGroup).Count);
        Assert.Equal(1, response.AssetsWithWorkOrders);
        Assert.Equal(1, response.AssetsWithoutWorkOrders);
        Assert.Equal("A-1", Assert.Single(response.TopAssetsByWorkOrderCount).AssetCode);
        Assert.Equal(KpiReliability.Yellow, response.TopAssetsReliability);
    }

    [Fact]
    public async Task Asset_pareto_applies_server_side_top_and_calculates_shares()
    {
        var commandCapture = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commandCapture);
        var (building, location, group, firstAsset, secondAsset) =
            await SeedAssetDimensionsAsync(database);
        var thirdAsset = Asset("A-3", "Third Asset", building, location, group);
        database.Context.Assets.Add(thirdAsset);
        await database.Context.SaveChangesAsync();

        database.Context.WorkOrders.AddRange(
            WorkOrder("WO-1", new DateTime(2026, 1, 1), firstAsset, building, location),
            WorkOrder("WO-2", new DateTime(2026, 1, 2), firstAsset, building, location),
            WorkOrder("WO-3", new DateTime(2026, 1, 31, 23, 59, 59), firstAsset, building, location),
            WorkOrder("WO-4", new DateTime(2026, 1, 3), secondAsset, building, location),
            WorkOrder("WO-5", new DateTime(2026, 1, 4), secondAsset, building, location),
            WorkOrder("WO-6", new DateTime(2026, 1, 5), thirdAsset, building, location));
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "HISTORICAL-ONLY",
            ReportedDateTime = new DateTime(2026, 1, 6),
            Discipline = "Electrical",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        commandCapture.Commands.Clear();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetMaintenanceActivityParetoAsync(
            new AssetMaintenanceActivityParetoQuery
            {
                DateFrom = new DateOnly(2026, 1, 1),
                DateTo = new DateOnly(2026, 1, 31),
                Top = 2
            });

        Assert.Equal(6, response.TotalWorkOrders);
        Assert.Equal(3, response.AssetsWithWorkOrders);
        Assert.Equal(2, response.AppliedTop);
        Assert.Collection(
            response.TopAssets,
            item =>
            {
                Assert.Equal("A-1", item.AssetCode);
                Assert.Equal(3, item.WorkOrderCount);
                Assert.Equal(50m, item.SharePercent);
                Assert.Equal(50m, item.CumulativeSharePercent);
            },
            item =>
            {
                Assert.Equal("A-2", item.AssetCode);
                Assert.Equal(2, item.WorkOrderCount);
                Assert.Equal(33.3333m, item.SharePercent);
                Assert.Equal(83.3333m, item.CumulativeSharePercent);
            });
        Assert.Equal(KpiReliability.Yellow, response.Metadata.Reliability);
        Assert.Contains(commandCapture.Commands, command =>
            command.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase)
            && command.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase)
            && command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Asset_pareto_uses_deterministic_tie_order_and_returns_empty_for_no_match()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, firstAsset, secondAsset) =
            await SeedAssetDimensionsAsync(database);
        database.Context.WorkOrders.AddRange(
            WorkOrder("WO-2", new DateTime(2026, 1, 2), secondAsset, building, location),
            WorkOrder("WO-1", new DateTime(2026, 1, 1), firstAsset, building, location));
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var tied = await service.GetMaintenanceActivityParetoAsync(
            new AssetMaintenanceActivityParetoQuery());
        Assert.Collection(
            tied.TopAssets,
            item => Assert.Equal("A-1", item.AssetCode),
            item => Assert.Equal("A-2", item.AssetCode));

        var empty = await service.GetMaintenanceActivityParetoAsync(
            new AssetMaintenanceActivityParetoQuery
            {
                DateFrom = new DateOnly(2025, 1, 1),
                DateTo = new DateOnly(2025, 1, 31)
            });
        Assert.Equal(0, empty.TotalWorkOrders);
        Assert.Equal(0, empty.AssetsWithWorkOrders);
        Assert.Empty(empty.TopAssets);
        Assert.Null(empty.Metadata.ActualMinDate);
    }

    [Fact]
    public async Task Work_order_overview_uses_exact_filters_and_excludes_historical_rows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, asset, _) = await SeedAssetDimensionsAsync(database);

        database.Context.WorkOrders.AddRange(
            WorkOrder(
                "WO-1",
                new DateTime(2026, 1, 31, 23, 59, 59),
                asset,
                building,
                location,
                discipline: "Electrical"),
            WorkOrder(
                "WO-2",
                new DateTime(2026, 2, 1, 0, 0, 0),
                asset,
                building,
                location,
                discipline: "electrical"));
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "WO-1",
            ReportedDateTime = new DateTime(2026, 1, 31, 23, 59, 59),
            Description = "Historical copy",
            Discipline = "Electrical",
            LocationNameRaw = location.Name,
            ResolutionDurationRaw = "20",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetOverviewAsync(new WorkOrderAnalyticsQuery
        {
            DateFrom = new DateOnly(2026, 1, 31),
            DateTo = new DateOnly(2026, 1, 31),
            Discipline = "Electrical"
        });

        Assert.Equal(1, response.TotalWorkOrders);
        Assert.Equal(0, response.OpenWorkOrders);
        Assert.Equal(1, response.ClosedWorkOrders);
        Assert.Equal(0, response.OtherWorkOrders);
        Assert.Equal("Electrical", Assert.Single(response.ByDiscipline).Category);
        Assert.Equal("Corrective", Assert.Single(response.ByWorkType).Category);
        Assert.Equal("Closed", Assert.Single(response.ByStatus).Category);
        Assert.Equal("Request", Assert.Single(response.ByFailureType).Category);
        Assert.Equal(1, Assert.Single(response.ByBuilding).Count);
        Assert.Equal(1, Assert.Single(response.ByLocation).Count);
        Assert.Equal(KpiReliability.Yellow, response.ByBuildingReliability);
    }

    [Fact]
    public async Task Work_order_overview_returns_empty_aggregations_for_no_match()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new EfAnalyticsQueryService(database.Context);

        var response = await service.GetOverviewAsync(new WorkOrderAnalyticsQuery
        {
            Discipline = "Does not exist"
        });

        Assert.Equal(0, response.TotalWorkOrders);
        Assert.Empty(response.ByDiscipline);
        Assert.Null(response.Metadata.ActualMinDate);
        Assert.Null(response.Metadata.ActualMaxDate);
    }

    [Fact]
    public async Task Work_order_open_and_closed_counts_use_raw_source_state_not_workflow_status()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, asset, _) = await SeedAssetDimensionsAsync(database);
        var sourceOpen = WorkOrder("WO-OPEN", DateTime.Today, asset, building, location);
        sourceOpen.RawStatusCode = "A";
        sourceOpen.Status = "Kapalı";
        var sourceClosed = WorkOrder("WO-CLOSED", DateTime.Today, asset, building, location);
        sourceClosed.RawStatusCode = "K";
        sourceClosed.Status = "Açık İş Emri";
        var other = WorkOrder("WO-OTHER", DateTime.Today, asset, building, location);
        other.RawStatusCode = "I";
        database.Context.WorkOrders.AddRange(sourceOpen, sourceClosed, other);
        await database.Context.SaveChangesAsync();

        var response = await new EfAnalyticsQueryService(database.Context)
            .GetOverviewAsync(new WorkOrderAnalyticsQuery());

        Assert.Equal(3, response.TotalWorkOrders);
        Assert.Equal(1, response.OpenWorkOrders);
        Assert.Equal(1, response.ClosedWorkOrders);
        Assert.Equal(1, response.OtherWorkOrders);
    }

    [Fact]
    public async Task Work_order_trend_groups_by_month_orders_points_and_applies_filters()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, asset, _) = await SeedAssetDimensionsAsync(database);
        database.Context.WorkOrders.AddRange(
            WorkOrder("WO-3", new DateTime(2026, 3, 1), asset, building, location),
            WorkOrder("WO-1", new DateTime(2026, 1, 1), asset, building, location),
            WorkOrder("WO-2", new DateTime(2026, 1, 15), asset, building, location),
            WorkOrder(
                "WO-OTHER",
                new DateTime(2026, 2, 1),
                asset,
                building,
                location,
                discipline: "Mechanical"));
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetTrendAsync(new WorkOrderAnalyticsQuery
        {
            Discipline = "Electrical"
        });

        Assert.Collection(
            response.Points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 1), point.Period);
                Assert.Equal(2, point.Count);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 3, 1), point.Period);
                Assert.Equal(1, point.Count);
            });
        Assert.Equal(3, response.Metadata.MatchedRecordCount);
    }

    [Fact]
    public async Task Work_order_activity_uses_canonical_rows_and_excludes_legacy_snapshot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, asset, _) = await SeedAssetDimensionsAsync(database);
        database.Context.WorkOrders.AddRange(
            WorkOrder("W-1", new DateTime(2026, 1, 1), asset, building, location, "Electrical"),
            WorkOrder("W-2", new DateTime(2026, 1, 31, 23, 59, 59), asset, building, location, "Mechanical"),
            WorkOrder("W-3", new DateTime(2026, 2, 1), asset, building, location, "Electrical"));
        database.Context.HistoricalWorkOrders.Add(
            Historical("LEGACY", new DateTime(2026, 2, 2), "LegacyOnly"));
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetActivityAsync(new WorkOrderActivityQuery());

        Assert.Equal(3, response.Metadata.MatchedRecordCount);
        Assert.Equal(3, response.Metadata.ValidRecordCount);
        Assert.Equal(0, response.Metadata.ExcludedByQualityCount);
        Assert.Collection(
            response.Trend,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 1), point.Period);
                Assert.Equal(2, point.Count);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 2, 1), point.Period);
                Assert.Equal(1, point.Count);
            });
        Assert.Contains(response.ByDiscipline, item =>
            item.Category == "Electrical" && item.Count == 2);
        Assert.Contains(response.ByDiscipline, item =>
            item.Category == "Mechanical" && item.Count == 1);
        Assert.DoesNotContain(response.ByDiscipline, item => item.Category == "LegacyOnly");
        Assert.Equal("core.WorkOrders", response.Metadata.SourceDataset);
        Assert.Equal(KpiReliability.Green, response.Metadata.Reliability);
    }

    [Fact]
    public async Task Work_order_activity_applies_exact_filter_and_returns_empty_for_no_match()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (building, location, _, asset, _) = await SeedAssetDimensionsAsync(database);
        database.Context.WorkOrders.AddRange(
            WorkOrder("W-1", new DateTime(2026, 1, 31, 23, 59, 59), asset, building, location, "Electrical"),
            WorkOrder("W-2", new DateTime(2026, 2, 1), asset, building, location, "Mechanical"));
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var filtered = await service.GetActivityAsync(new WorkOrderActivityQuery
        {
            DateFrom = new DateOnly(2026, 1, 31),
            DateTo = new DateOnly(2026, 1, 31),
            Discipline = "Electrical"
        });
        Assert.Equal(1, filtered.Metadata.MatchedRecordCount);
        Assert.Equal("Electrical", filtered.AppliedDiscipline);
        Assert.Equal("Electrical", Assert.Single(filtered.ByDiscipline).Category);

        var empty = await service.GetActivityAsync(new WorkOrderActivityQuery
        {
            Discipline = "NoMatch"
        });
        Assert.Equal(0, empty.Metadata.MatchedRecordCount);
        Assert.Empty(empty.Trend);
        Assert.Empty(empty.ByDiscipline);
        Assert.Null(empty.Metadata.ActualMinDate);
    }

    [Fact]
    public async Task Scada_overview_counts_source_occurrences_and_timestamp_quality()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        SeedScadaEvents(database);
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetOverviewAsync(new ScadaAnalyticsQuery());

        Assert.Equal(4, response.TotalAlarmOccurrences);
        Assert.Equal(2, response.InvalidOrMissingTimestampCount);
        Assert.Equal(2, response.DateQualityIssueCount);
        Assert.Contains(response.BySourceSheet, item =>
            item.Category == "MEKANİK" && item.Count == 3);
        Assert.Contains(response.ByAlarmType, item =>
            item.Category == "Cooling" && item.Count == 2);
        Assert.Equal(2, response.Metadata.ValidRecordCount);
        Assert.Equal(2, response.Metadata.ExcludedByQualityCount);
    }

    [Fact]
    public async Task Scada_overview_date_filter_excludes_null_received_at()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        SeedScadaEvents(database);
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetOverviewAsync(new ScadaAnalyticsQuery
        {
            DateFrom = new DateOnly(2026, 1, 1),
            DateTo = new DateOnly(2026, 1, 31),
            SourceSheet = "MEKANİK"
        });

        Assert.Equal(1, response.TotalAlarmOccurrences);
        Assert.Equal(0, response.InvalidOrMissingTimestampCount);
        Assert.Equal("MEKANİK", Assert.Single(response.BySourceSheet).Category);
    }

    [Fact]
    public async Task Scada_trend_uses_only_valid_received_at_and_reports_exclusions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        SeedScadaEvents(database);
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetTrendAsync(new ScadaAnalyticsQuery());

        Assert.Collection(
            response.Points,
            point =>
            {
                Assert.Equal(new DateOnly(2026, 1, 1), point.Period);
                Assert.Equal(1, point.Count);
            },
            point =>
            {
                Assert.Equal(new DateOnly(2026, 2, 1), point.Period);
                Assert.Equal(1, point.Count);
            });
        Assert.Equal(4, response.Metadata.MatchedRecordCount);
        Assert.Equal(2, response.Quality.ValidRecordCount);
        Assert.Equal(2, response.Quality.ExcludedByQualityCount);
        Assert.Equal(KpiReliability.Yellow, response.Metadata.Reliability);
    }

    [Fact]
    public async Task Import_quality_overview_aggregates_audit_status_and_fingerprint_versions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var completedBatch = ImportBatch("Asset", "Completed");
        var failedBatch = ImportBatch("WorkOrder", "Failed");
        database.Context.ImportBatches.AddRange(completedBatch, failedBatch);
        await database.Context.SaveChangesAsync();

        database.Context.ImportSourceRecords.AddRange(
            SourceRecord(completedBatch, "Succeeded", null),
            SourceRecord(completedBatch, "Duplicate", "asset/v1"),
            SourceRecord(failedBatch, "Failed", "work-order/v1"));
        database.Context.ImportErrors.Add(new ImportError
        {
            ImportBatchId = failedBatch.Id,
            RowNumber = 2,
            ErrorMessage = "Invalid row"
        });
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var response = await service.GetOverviewAsync();

        Assert.Equal(2, response.TotalBatches);
        Assert.Contains(response.BatchesByStatus, item =>
            item.Category == "Completed" && item.Count == 1);
        Assert.Contains(response.SourceRecordsByParseStatus, item =>
            item.Category == "Failed" && item.Count == 1);
        Assert.Equal(1, response.ImportErrorCount);
        Assert.Equal("WorkOrder", Assert.Single(response.ErrorsBySourceType).SourceType);
        Assert.Equal(1, response.LegacySourceRecordCount);
        Assert.Equal(2, response.VersionedSourceRecordCount);
        Assert.Contains(response.FingerprintAlgorithmDistribution, item =>
            item.Category == "<Legacy>" && item.Count == 1);
    }

    private static async Task<(
        Building Building,
        Location Location,
        AssetGroup Group,
        Asset FirstAsset,
        Asset SecondAsset)> SeedAssetDimensionsAsync(SqliteTestDatabase database)
    {
        var building = new Building { Code = "B-1", Name = "Building One" };
        var location = new Location { Name = "Location One", Building = building };
        var group = new AssetGroup { Code = "G-1", Name = "Group One" };
        var firstAsset = Asset("A-1", "First Asset", building, location, group);
        var secondAsset = Asset("A-2", "Second Asset", building, location, group);
        database.Context.Assets.AddRange(firstAsset, secondAsset);
        await database.Context.SaveChangesAsync();
        return (building, location, group, firstAsset, secondAsset);
    }

    private static Asset Asset(
        string code,
        string name,
        Building building,
        Location location,
        AssetGroup group) =>
        new()
        {
            AssetCode = code,
            Name = name,
            AssetType = "Equipment",
            Status = "In Use",
            Building = building,
            Location = location,
            AssetGroup = group
            ,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static WorkOrder WorkOrder(
        string number,
        DateTime reportedAt,
        Asset asset,
        Building building,
        Location location,
        string discipline = "Electrical") =>
        new()
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt,
            Asset = asset,
            Building = building,
            Location = location,
            Description = $"Description {number}",
            Discipline = discipline,
            RawStatusCode = "K",
            Status = "Closed",
            WorkType = "Corrective",
            FailureType = "Request",
            FailureReason = "Reason"
            ,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static HistoricalWorkOrder Historical(
        string sourceReference,
        DateTime? reportedAt,
        string discipline) =>
        new()
        {
            SourceReference = sourceReference,
            ReportedDateTime = reportedAt,
            Description = $"Description {sourceReference}",
            Discipline = discipline,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static void SeedScadaEvents(SqliteTestDatabase database)
    {
        database.Context.ScadaAlarmEvents.AddRange(
            ScadaEvent(
                "MEKANİK",
                "Cooling",
                new DateTime(2026, 1, 31, 23, 59, 59),
                "Received:Parsed;Cleared:Parsed"),
            ScadaEvent(
                "MEKANİK",
                "Cooling",
                new DateTime(2026, 2, 1),
                "Received:Parsed;Cleared:Missing"),
            ScadaEvent(
                "MEKANİK",
                "Heating",
                null,
                "Received:InvalidTime;Cleared:Missing"),
            ScadaEvent(
                "YANGIN",
                "Fire",
                new DateTime(2204, 1, 1),
                "Received:SuspiciousYear;Cleared:SuspiciousYear"));
    }

    private static ScadaAlarmEvent ScadaEvent(
        string sourceSheet,
        string alarmType,
        DateTime? receivedAt,
        string parseStatus) =>
        new()
        {
            SourceSheet = sourceSheet,
            SectionRaw = "Section",
            LocationRaw = "Location",
            AlarmType = alarmType,
            InterventionLevel = "Normal",
            Description = "Alarm description",
            ReceivedAt = receivedAt,
            DateTimeParseStatus = parseStatus,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static ImportBatch ImportBatch(string sourceType, string status) =>
        new()
        {
            SourceType = sourceType,
            FileName = $"{sourceType}.xlsx",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = status == "Completed" ? DateTimeOffset.UtcNow : null,
            Status = status
        };

    private static ImportSourceRecord SourceRecord(
        ImportBatch batch,
        string parseStatus,
        string? algorithm) =>
        new()
        {
            ImportBatchId = batch.Id,
            SourceSheet = "Sheet1",
            SourceRowNumber = 2,
            RowFingerprint = Guid.NewGuid().ToString("N"),
            IdempotencyFingerprint = algorithm == null ? null : Guid.NewGuid().ToString("N"),
            FingerprintAlgorithm = algorithm,
            RawData = "{}",
            ParseStatus = parseStatus,
            CreatedAt = DateTimeOffset.UtcNow
        };
}

internal sealed class CommandCaptureInterceptor : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
