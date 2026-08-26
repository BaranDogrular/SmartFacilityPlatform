using System.Text.Json;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class EarlyWarningTests
{
    [Fact]
    public void Score_is_deterministic_bounded_and_handles_zero_denominators()
    {
        var signals = new EarlyWarningSignals(3, 0, 3, 0, 5, 0, 0, 0.5m, 0.5m);

        var first = EarlyWarningScoring.Calculate(signals);
        var second = EarlyWarningScoring.Calculate(signals);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Deviation, second.Deviation);
        Assert.Equal(first.Components, second.Components);
        Assert.Equal(first.Reasons, second.Reasons);
        Assert.InRange(first.Score, 0m, 100m);
        Assert.Equal(87m, first.Score);
        Assert.Equal(EarlyWarningLevel.High, first.Level);
        Assert.Contains(first.Reasons, reason => reason.Contains("yeni aktivite"));
        Assert.InRange(first.Components.Total, 0m, 100m);
    }

    [Fact]
    public void High_but_stable_activity_is_not_automatically_an_early_warning()
    {
        var stableHighVolume = EarlyWarningScoring.Calculate(
            new EarlyWarningSignals(30, 35, 120, 118, 360, 0, 0, 115m, 10m));
        var lowVolumeSharpIncrease = EarlyWarningScoring.Calculate(
            new EarlyWarningSignals(3, 0, 3, 0, 5, 0, 0, 0.5m, 0.5m));

        Assert.Equal(EarlyWarningLevel.Normal, stableHighVolume.Level);
        Assert.True(stableHighVolume.Score < EarlyWarningScoring.MediumThreshold);
        Assert.Equal(EarlyWarningLevel.High, lowVolumeSharpIncrease.Level);
        Assert.True(lowVolumeSharpIncrease.Score > stableHighVolume.Score);
    }

    [Theory]
    [InlineData(60, EarlyWarningLevel.High)]
    [InlineData(59.99, EarlyWarningLevel.Medium)]
    [InlineData(30, EarlyWarningLevel.Medium)]
    [InlineData(29.99, EarlyWarningLevel.Normal)]
    [InlineData(0, EarlyWarningLevel.Normal)]
    public void Warning_levels_use_documented_thresholds(
        decimal score,
        EarlyWarningLevel expected) =>
        Assert.Equal(expected, EarlyWarningScoring.GetLevel(score));

    [Fact]
    public async Task Query_uses_exact_windows_excludes_future_unlinked_and_legacy_and_marks_insufficient_baseline()
    {
        var commandCapture = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commandCapture);
        var established = Asset("A-1", "Established");
        var newAsset = Asset("A-2", "New");
        database.Context.Assets.AddRange(newAsset, established);

        for (var month = 7; month <= 12; month++)
        {
            database.Context.WorkOrders.Add(
                WorkOrder($"BASE-{month}", new DateTime(2025, month, 10), established));
        }

        database.Context.WorkOrders.AddRange(
            WorkOrder("LAST-7", new DateTime(2026, 8, 19), established),
            WorkOrder("PREVIOUS-7-START", new DateTime(2026, 8, 12), established),
            WorkOrder("PREVIOUS-7-END", new DateTime(2026, 8, 18, 23, 59, 59), established),
            WorkOrder("LAST-30-START", new DateTime(2026, 7, 27), established),
            WorkOrder("PREVIOUS-30-END", new DateTime(2026, 7, 26, 23, 59, 59), established),
            WorkOrder("PREVIOUS-30-START", new DateTime(2026, 6, 27), established),
            WorkOrder("OPEN-BY-RAW", new DateTime(2026, 5, 1), established, "A", "Closed workflow"),
            WorkOrder("WORKFLOW-OPEN-ONLY", new DateTime(2026, 8, 20), established, "K", "Açık İş Emri"),
            WorkOrder("FUTURE", new DateTime(2026, 8, 26), established),
            WorkOrder("NEW-ASSET", new DateTime(2026, 8, 25), newAsset));
        database.Context.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNumber = "UNLINKED",
            ReportedDateTime = new DateTime(2026, 8, 25),
            AssetCodeRaw = "RAW-ONLY",
            RawStatusCode = "A",
            Status = "Closed",
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
        var query = new EarlyWarningQuery { AsOf = new DateOnly(2026, 8, 25), Top = 10 };
        var first = await service.GetEarlyWarningAsync(query);
        var second = await service.GetEarlyWarningAsync(query);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(new DateOnly(2025, 7, 1), first.Metadata.BaselineWindow!.From);
        Assert.Equal(new DateOnly(2026, 6, 30), first.Metadata.BaselineWindow.Through);
        Assert.Equal(2, first.Metadata.TotalAssetsConsidered);
        Assert.Equal(1, first.Metadata.EligibleAssets);
        Assert.Equal(1, first.Metadata.InsufficientBaselineAssets);
        Assert.Equal(1, first.Metadata.ExcludedUnlinkedWorkOrders);
        Assert.Equal(2, first.Items.Count);

        var item = first.Items[0];
        Assert.Equal("A-1", item.AssetCode);
        Assert.Equal(EarlyWarningBaselineStatus.Sufficient, item.BaselineStatus);
        Assert.Equal(2, item.Last7Count);
        Assert.Equal(2, item.Previous7Count);
        Assert.Equal(5, item.Last30Count);
        Assert.Equal(2, item.Previous30Count);
        Assert.Equal(1, item.OpenCount);
        Assert.Equal(1m, item.BaselineMedian);
        Assert.DoesNotContain(item.Reasons, reason => reason.Contains("FUTURE"));

        Assert.Equal(EarlyWarningBaselineStatus.InsufficientBaseline, first.Items[1].BaselineStatus);
        Assert.Null(first.Items[1].WarningScore);
        Assert.Null(first.Items[1].WarningLevel);
        Assert.Contains(first.Items[1].Reasons, reason => reason.Contains("aktif ay"));
        Assert.Equal(8, commandCapture.Commands.Count);
        Assert.DoesNotContain(
            commandCapture.Commands,
            command => command.Contains("HistoricalWorkOrders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_returns_empty_metadata_for_empty_dataset_and_propagates_cancellation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new EfAnalyticsQueryService(database.Context);

        var empty = await service.GetEarlyWarningAsync(new EarlyWarningQuery());

        Assert.Null(empty.Metadata.AsOf);
        Assert.Null(empty.Metadata.BaselineWindow);
        Assert.Equal(0, empty.Metadata.TotalAssetsConsidered);
        Assert.Empty(empty.Items);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetEarlyWarningAsync(
                new EarlyWarningQuery { AsOf = new DateOnly(2026, 8, 25) },
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
