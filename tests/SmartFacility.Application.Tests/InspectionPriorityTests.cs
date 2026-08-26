using System.Text.Json;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class InspectionPriorityTests
{
    [Fact]
    public void Score_is_deterministic_bounded_and_safe_when_previous_period_is_zero()
    {
        var signals = new InspectionPrioritySignals(20, 40, 0, 80, 8);

        var first = InspectionPriorityScoring.Calculate(signals);
        var second = InspectionPriorityScoring.Calculate(signals);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.ActivityChange, second.ActivityChange);
        Assert.Equal(first.Reasons, second.Reasons);
        Assert.Equal(100m, first.Score);
        Assert.Equal(InspectionPriorityLevel.High, first.Level);
        Assert.Equal(40, first.ActivityChange);
        Assert.InRange(first.Score, 0m, 100m);
        Assert.Contains(first.Reasons, reason => reason.Contains("aktivitesi başladı"));
    }

    [Theory]
    [InlineData(50, InspectionPriorityLevel.High)]
    [InlineData(49.99, InspectionPriorityLevel.Medium)]
    [InlineData(25, InspectionPriorityLevel.Medium)]
    [InlineData(24.99, InspectionPriorityLevel.Low)]
    [InlineData(0, InspectionPriorityLevel.Low)]
    public void Priority_levels_use_documented_thresholds(
        decimal score,
        InspectionPriorityLevel expected) =>
        Assert.Equal(expected, InspectionPriorityScoring.GetLevel(score));

    [Fact]
    public async Task Query_uses_non_overlapping_windows_raw_open_state_and_excludes_future_unlinked_and_legacy_rows()
    {
        var commandCapture = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commandCapture);
        var assetA = Asset("A-1", "First");
        var assetB = Asset("A-2", "Second");
        database.Context.Assets.AddRange(assetB, assetA);
        database.Context.WorkOrders.AddRange(
            WorkOrder("LAST-7-BOUNDARY", new DateTime(2026, 8, 19), assetA),
            WorkOrder("LAST-30-BOUNDARY", new DateTime(2026, 7, 27), assetA),
            WorkOrder("PREVIOUS-30-END", new DateTime(2026, 7, 26, 23, 59, 59), assetA),
            WorkOrder("PREVIOUS-30-START", new DateTime(2026, 6, 27), assetA),
            WorkOrder("OPEN-BY-RAW", new DateTime(2026, 5, 1), assetA, "A", "Closed workflow"),
            WorkOrder("WORKFLOW-OPEN-ONLY", new DateTime(2026, 8, 20), assetA, "K", "Açık İş Emri"),
            WorkOrder("FUTURE", new DateTime(2026, 8, 26), assetA, "A", "Closed workflow"),
            WorkOrder("TIE-A", new DateTime(2026, 8, 25), assetB));
        database.Context.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNumber = "UNLINKED",
            ReportedDateTime = new DateTime(2026, 8, 25),
            AssetCodeRaw = "RAW-ONLY",
            RawStatusCode = "A",
            Status = "Closed workflow",
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "LEGACY",
            ReportedDateTime = new DateTime(2026, 8, 25),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        commandCapture.Commands.Clear();

        var service = new EfAnalyticsQueryService(database.Context);
        var query = new InspectionPriorityQuery
        {
            AsOf = new DateOnly(2026, 8, 25),
            Top = 10
        };
        var first = await service.GetInspectionPriorityAsync(query);
        var second = await service.GetInspectionPriorityAsync(query);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(7, first.Metadata.EligibleWorkOrders);
        Assert.Equal(1, first.Metadata.ExcludedUnlinkedWorkOrders);
        Assert.Equal(87.5m, first.Metadata.CoveragePercent);
        Assert.Equal(2, first.Metadata.TotalAssetsEvaluated);
        Assert.Equal(new DateOnly(2026, 8, 25), first.Metadata.AsOf);
        Assert.DoesNotContain(first.Metadata.Notes, note => note.Contains("HistoricalWorkOrders snapshot is queried"));

        var item = Assert.Single(first.Items, candidate => candidate.AssetCode == "A-1");
        Assert.Equal(2, item.Last7Count);
        Assert.Equal(3, item.Last30Count);
        Assert.Equal(2, item.Previous30Count);
        Assert.Equal(5, item.Last90Count);
        Assert.Equal(1, item.OpenCount);
        Assert.Equal(1, item.ActivityChange);
        Assert.Contains(item.Reasons, reason => reason == "1 açık iş emri bulunuyor");
        Assert.DoesNotContain(item.Reasons, reason => reason.Contains("FUTURE"));
        Assert.Equal(6, commandCapture.Commands.Count);
        Assert.DoesNotContain(
            commandCapture.Commands,
            command => command.Contains("HistoricalWorkOrders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_returns_zero_coverage_empty_result_for_an_empty_canonical_dataset()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var result = await new EfAnalyticsQueryService(database.Context)
            .GetInspectionPriorityAsync(new InspectionPriorityQuery());

        Assert.Null(result.Metadata.AsOf);
        Assert.Null(result.Metadata.AnalysisWindow);
        Assert.Equal(0, result.Metadata.EligibleWorkOrders);
        Assert.Equal(0, result.Metadata.ExcludedUnlinkedWorkOrders);
        Assert.Equal(0m, result.Metadata.CoveragePercent);
        Assert.Equal(0, result.Metadata.TotalAssetsEvaluated);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Query_orders_ties_by_asset_code_and_returns_empty_without_recent_or_open_activity()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var assetB = Asset("B-2", "Second");
        var assetA = Asset("A-1", "First");
        var oldAsset = Asset("OLD", "Old");
        database.Context.Assets.AddRange(assetB, assetA, oldAsset);
        database.Context.WorkOrders.AddRange(
            WorkOrder("A", new DateTime(2026, 8, 25), assetA),
            WorkOrder("B", new DateTime(2026, 8, 25), assetB),
            WorkOrder("OLD", new DateTime(2020, 1, 1), oldAsset));
        await database.Context.SaveChangesAsync();

        var service = new EfAnalyticsQueryService(database.Context);
        var ranked = await service.GetInspectionPriorityAsync(new InspectionPriorityQuery
        {
            AsOf = new DateOnly(2026, 8, 25),
            Top = 2
        });
        Assert.Collection(
            ranked.Items,
            item => Assert.Equal("A-1", item.AssetCode),
            item => Assert.Equal("B-2", item.AssetCode));

        var empty = await service.GetInspectionPriorityAsync(new InspectionPriorityQuery
        {
            AsOf = new DateOnly(2025, 1, 1)
        });
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.Metadata.EligibleWorkOrders);
        Assert.Equal(0, empty.Metadata.TotalAssetsEvaluated);
    }

    [Fact]
    public async Task Query_propagates_cancellation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new EfAnalyticsQueryService(database.Context).GetInspectionPriorityAsync(
                new InspectionPriorityQuery { AsOf = new DateOnly(2026, 8, 25) },
                cancellation.Token));
    }

    private static Asset Asset(string code, string name) =>
        new()
        {
            AssetCode = code,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static WorkOrder WorkOrder(
        string number,
        DateTime reportedAt,
        Asset asset,
        string rawStatusCode = "K",
        string workflowStatus = "Closed") =>
        new()
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt,
            Asset = asset,
            AssetCodeRaw = asset.AssetCode,
            RawStatusCode = rawStatusCode,
            Status = workflowStatus,
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
