using Microsoft.EntityFrameworkCore;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class Asset360SummaryTests
{
    [Fact]
    public async Task Summary_uses_bounded_canonical_aggregates_and_existing_scorers()
    {
        var commandCapture = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commandCapture);
        var (asset, building, location) = await SeedAssetAsync(database);

        var baselineDates = new[]
        {
            new DateTime(2025, 7, 5),
            new DateTime(2025, 8, 5),
            new DateTime(2025, 9, 5),
            new DateTime(2025, 10, 5),
            new DateTime(2025, 11, 5),
            new DateTime(2025, 12, 5)
        };
        database.Context.WorkOrders.AddRange(baselineDates.Select((date, index) =>
            WorkOrder($"BASE-{index}", date, asset, building, location)));
        database.Context.WorkOrders.AddRange(
            WorkOrder("WO-1", new DateTime(2026, 8, 25, 10, 0, 0), asset, building, location, true),
            WorkOrder("WO-2", new DateTime(2026, 8, 19, 0, 0, 0), asset, building, location),
            WorkOrder("WO-3", new DateTime(2026, 8, 18, 23, 59, 59), asset, building, location),
            WorkOrder("WO-4", new DateTime(2026, 7, 27, 0, 0, 0), asset, building, location),
            WorkOrder("WO-5", new DateTime(2026, 7, 26, 23, 59, 59), asset, building, location));
        database.Context.WorkOrders.Add(new WorkOrder
        {
            WorkOrderNumber = "UNLINKED",
            ReportedDateTime = new DateTime(2026, 8, 24),
            RawStatusCode = "K",
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var nonCanonical = WorkOrder(
            "NON-CANONICAL",
            new DateTime(2026, 8, 25, 12, 0, 0),
            asset,
            building,
            location);
        nonCanonical.IsInCanonicalSnapshot = false;
        database.Context.WorkOrders.Add(nonCanonical);
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "LEGACY-ONLY",
            ReportedDateTime = new DateTime(2026, 8, 25),
            Discipline = "Electrical",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        commandCapture.Commands.Clear();

        var response = await new EfAnalyticsQueryService(database.Context)
            .GetAsset360SummaryAsync(asset.Id);

        Assert.NotNull(response);
        Assert.Equal(new DateOnly(2026, 8, 25), response.AsOf);
        Assert.Equal(asset.Id, response.Identity.AssetId);
        Assert.Equal("A-360", response.Identity.AssetCode);
        Assert.Equal("Building 360", response.Identity.BuildingName);
        Assert.Equal("Location 360", response.Identity.LocationName);
        Assert.Equal("Group 360", response.Identity.AssetGroupName);
        Assert.Equal(11, response.Maintenance.TotalWorkOrders);
        Assert.Equal(1, response.Maintenance.OpenWorkOrders);
        Assert.Equal(2, response.Maintenance.Last7Count);
        Assert.Equal(4, response.Maintenance.Last30Count);
        Assert.Equal(5, response.Maintenance.Last90Count);
        Assert.Equal(new DateTime(2026, 8, 25, 10, 0, 0), response.Maintenance.LastWorkOrderDate);

        var expectedPriority = InspectionPriorityScoring.Calculate(
            new InspectionPrioritySignals(2, 4, 1, 5, 1));
        Assert.Equal(expectedPriority.Score, response.InspectionPriority.Score);
        Assert.Equal(expectedPriority.Level, response.InspectionPriority.Level);
        Assert.Equal(expectedPriority.Reasons, response.InspectionPriority.Reasons);
        Assert.Equal(InspectionPriorityScoring.Version, response.InspectionPriority.ScoringVersion);

        var expectedEarlyWarning = EarlyWarningScoring.Calculate(
            new EarlyWarningSignals(2, 1, 4, 1, 5, 1, 0, 0.5m, 0.5m));
        Assert.Equal(EarlyWarningBaselineStatus.Sufficient, response.EarlyWarning.BaselineStatus);
        Assert.Equal(expectedEarlyWarning.Score, response.EarlyWarning.Score);
        Assert.Equal(expectedEarlyWarning.Level, response.EarlyWarning.Level);
        Assert.Equal(expectedEarlyWarning.Reasons, response.EarlyWarning.Reasons);
        Assert.NotNull(response.EarlyWarning.Components);
        Assert.Equal(
            expectedEarlyWarning.Components.Total,
            response.EarlyWarning.Components.Acceleration
            + response.EarlyWarning.Components.ShortTermSpike
            + response.EarlyWarning.Components.HistoricalDeviation
            + response.EarlyWarning.Components.RecurrenceBurst
            + response.EarlyWarning.Components.OpenEmergence);

        Assert.Equal(11, response.Scope.LinkedCanonicalWorkOrders);
        Assert.Equal(1, response.Scope.ExcludedUnlinkedCanonicalWorkOrders);
        Assert.True(response.Scope.HistoricalWorkOrdersExcluded);
        Assert.True(response.Scope.ScadaAndOutagesExcluded);
        Assert.InRange(commandCapture.Commands.Count, 1, 5);
        var sql = string.Join(Environment.NewLine, commandCapture.Commands);
        Assert.DoesNotContain("HistoricalWorkOrders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HistoricalInterventions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RequestedByName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AssignedPersonnelName", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Summary_returns_zero_activity_decisions_for_valid_asset_without_work_orders()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var (asset, building, location) = await SeedAssetAsync(database);
        var otherAsset = new Asset
        {
            AssetCode = "OTHER",
            Name = "Other asset",
            Building = building,
            Location = location,
            AssetGroup = asset.AssetGroup,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        database.Context.Assets.Add(otherAsset);
        database.Context.WorkOrders.Add(WorkOrder(
            "OTHER-WO",
            new DateTime(2026, 8, 25),
            otherAsset,
            building,
            location));
        await database.Context.SaveChangesAsync();

        var response = await new EfAnalyticsQueryService(database.Context)
            .GetAsset360SummaryAsync(asset.Id);

        Assert.NotNull(response);
        Assert.Equal(0, response.Maintenance.TotalWorkOrders);
        Assert.Equal(0, response.Maintenance.OpenWorkOrders);
        Assert.Null(response.Maintenance.LastWorkOrderDate);
        Assert.Equal(0, response.InspectionPriority.Score);
        Assert.Equal(InspectionPriorityLevel.Low, response.InspectionPriority.Level);
        Assert.Empty(response.InspectionPriority.Reasons);
        Assert.Equal(EarlyWarningBaselineStatus.InsufficientBaseline, response.EarlyWarning.BaselineStatus);
        Assert.Null(response.EarlyWarning.Score);
        Assert.Null(response.EarlyWarning.Level);
    }

    [Fact]
    public async Task Summary_returns_null_for_unknown_asset()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var response = await new EfAnalyticsQueryService(database.Context)
            .GetAsset360SummaryAsync(404);

        Assert.Null(response);
    }

    private static async Task<(Asset Asset, Building Building, Location Location)>
        SeedAssetAsync(SqliteTestDatabase database)
    {
        var building = new Building { Code = "B-360", Name = "Building 360" };
        var location = new Location { Name = "Location 360", Building = building };
        var group = new AssetGroup { Code = "G-360", Name = "Group 360" };
        var asset = new Asset
        {
            AssetCode = "A-360",
            Name = "Asset 360",
            AssetType = "Equipment",
            Status = "In Use",
            SerialNumber = "SERIAL-360",
            LastMaintenanceDate = new DateTime(2026, 7, 1),
            Building = building,
            Location = location,
            AssetGroup = group,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        database.Context.Assets.Add(asset);
        await database.Context.SaveChangesAsync();
        return (asset, building, location);
    }

    private static WorkOrder WorkOrder(
        string number,
        DateTime reportedAt,
        Asset asset,
        Building building,
        Location location,
        bool open = false) =>
        new()
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt,
            Asset = asset,
            Building = building,
            Location = location,
            Description = $"Description {number}",
            Discipline = "Electrical",
            RawStatusCode = open ? "A" : "K",
            Status = open ? "Open" : "Closed",
            WorkType = "Corrective",
            FailureType = "Request",
            FailureReason = "Reason",
            IsInCanonicalSnapshot = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
