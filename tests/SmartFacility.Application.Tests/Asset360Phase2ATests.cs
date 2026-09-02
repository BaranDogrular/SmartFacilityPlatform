using System.Text.Json;
using System.Text.Json.Serialization;
using SmartFacility.Application.Analytics.Models;
using SmartFacility.Application.Analytics.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Analytics;

namespace SmartFacility.Application.Tests;

public sealed class Asset360Phase2ATests
{
    [Fact]
    public async Task Activity_is_bounded_deterministic_private_and_selects_one_intervention()
    {
        var commands = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commands);
        var snapshotBatch = Batch("WorkOrder");
        var interventionBatch = Batch("HistoricalIntervention");
        var asset = Asset("A-1", "Main pump");
        var otherAsset = Asset("B-1", "Other pump");
        database.Context.AddRange(snapshotBatch, interventionBatch, asset, otherAsset);

        var oldest = WorkOrder("WO-OLD", new DateTime(2026, 8, 1), asset, snapshotBatch);
        var tiedFirst = WorkOrder("WO-TIE-1", new DateTime(2026, 8, 20), asset, snapshotBatch);
        var tiedSecond = WorkOrder("WO-TIE-2", new DateTime(2026, 8, 20), asset, snapshotBatch);
        var missingDate = WorkOrder("WO-NULL", null, asset, snapshotBatch);
        var noncanonical = WorkOrder("WO-INACTIVE", new DateTime(2026, 8, 30), asset, snapshotBatch);
        noncanonical.IsInCanonicalSnapshot = false;
        var otherAssetWorkOrder = WorkOrder(
            "WO-OTHER-ASSET",
            new DateTime(2026, 8, 30),
            otherAsset,
            snapshotBatch);
        var unlinked = WorkOrder("WO-UNLINKED", new DateTime(2026, 8, 30), null, snapshotBatch);
        database.Context.WorkOrders.AddRange(
            oldest,
            tiedFirst,
            tiedSecond,
            missingDate,
            noncanonical,
            otherAssetWorkOrder,
            unlinked);
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "LEGACY",
            ReportedDateTime = new DateTime(2026, 8, 31),
            Description = "Must never appear",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        tiedSecond.Description =
            "<b>Pompa kontrolü</b> employee@example.com +90 532 123 45 67";
        tiedSecond.RequestedByName = "Requester Person";
        tiedSecond.AssignedPersonnelName = "Assigned Person";
        database.Context.HistoricalInterventions.AddRange(
            Intervention(
                tiedSecond,
                interventionBatch,
                HistoricalInterventionQuality.Generic,
                "Generic sanitized action",
                "RAW-GENERIC employee@example.com",
                new DateTime(2026, 8, 20, 13, 0, 0),
                "HI-GENERIC"),
            Intervention(
                tiedSecond,
                interventionBatch,
                HistoricalInterventionQuality.Informative,
                "Older informative sanitized action",
                "RAW-INFORMATIVE +90 532 123 45 67",
                new DateTime(2026, 8, 20, 12, 0, 0),
                "HI-INFORMATIVE"),
            Intervention(
                tiedSecond,
                interventionBatch,
                HistoricalInterventionQuality.Informative,
                "Newest informative sanitized action",
                "RAW-INFORMATIVE-NEW",
                new DateTime(2026, 8, 20, 14, 0, 0),
                "HI-INFORMATIVE-NEW"),
            Intervention(
                tiedSecond,
                interventionBatch,
                HistoricalInterventionQuality.NoAction,
                null,
                "RAW-NO-ACTION",
                null,
                "HI-NO-ACTION"));
        await database.Context.SaveChangesAsync();
        commands.Commands.Clear();

        var service = new EfAnalyticsQueryService(database.Context);
        var result = await service.GetAssetActivityAsync(
            asset.Id,
            new AssetActivityQuery { PageSize = 50 });

        Assert.Equal(AssetActivityResultStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(50, result.Response.PageSize);
        Assert.False(result.Response.HasNextPage);
        Assert.Equal(
            [tiedSecond.Id, tiedFirst.Id, oldest.Id, missingDate.Id],
            result.Response.Items.Select(item => item.WorkOrderId));
        Assert.DoesNotContain(result.Response.Items, item => item.WorkOrderId == noncanonical.Id);
        Assert.DoesNotContain(result.Response.Items, item => item.WorkOrderId == otherAssetWorkOrder.Id);
        Assert.DoesNotContain(result.Response.Items, item => item.WorkOrderId == unlinked.Id);
        Assert.Equal(4, result.Response.Items[0].InterventionCount);
        var selected = result.Response.Items[0].HistoricalIntervention;
        Assert.NotNull(selected);
        Assert.Equal(AssetActivityInterventionQuality.Informative, selected.Quality);
        Assert.Equal("Newest informative sanitized action", selected.WorkPerformedDescription);
        Assert.DoesNotContain("<b>", result.Response.Items[0].DescriptionSnippet);
        Assert.DoesNotContain("employee@example.com", result.Response.Items[0].DescriptionSnippet);
        Assert.DoesNotContain("532 123 45 67", result.Response.Items[0].DescriptionSnippet);
        Assert.Equal(AssetActivityState.Closed, result.Response.Items[0].State);
        Assert.Null(result.Response.Items[1].HistoricalIntervention);
        Assert.Equal(0, result.Response.Items[1].InterventionCount);
        Assert.Equal(4, commands.Commands.Count);
        var interventionCommand = Assert.Single(
            commands.Commands,
            command => command.Contains(
                "HistoricalInterventions",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains("WorkOrderId", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" IN ", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "AssetActivity.InterventionPage",
            interventionCommand,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            commands.Commands,
            command => command.Contains("HistoricalWorkOrders", StringComparison.OrdinalIgnoreCase));

        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        var payload = JsonSerializer.Serialize(result.Response, options);
        Assert.DoesNotContain("Requester Person", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Assigned Person", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW-", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceFile", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Intervention_lookup_is_one_page_bounded_query_for_at_most_fifty_work_orders()
    {
        var commands = new CommandCaptureInterceptor();
        await using var database = await SqliteTestDatabase.CreateAsync(commands);
        var snapshotBatch = Batch("WorkOrder");
        var interventionBatch = Batch("HistoricalIntervention");
        var asset = Asset("A-1", "Main pump");
        database.Context.AddRange(snapshotBatch, interventionBatch, asset);

        var workOrders = Enumerable.Range(1, 55)
            .Select(index => WorkOrder(
                $"WO-{index:00}",
                new DateTime(2026, 8, 20).AddMinutes(index),
                asset,
                snapshotBatch))
            .ToArray();
        database.Context.WorkOrders.AddRange(workOrders);
        await database.Context.SaveChangesAsync();
        database.Context.HistoricalInterventions.AddRange(workOrders.Select(item => Intervention(
            item,
            interventionBatch,
            HistoricalInterventionQuality.Informative,
            $"Sanitized action {item.WorkOrderNumber}",
            $"RAW-{item.WorkOrderNumber}",
            item.ReportedDateTime,
            $"HI-{item.WorkOrderNumber}")));
        await database.Context.SaveChangesAsync();
        commands.Commands.Clear();

        var result = await new EfAnalyticsQueryService(database.Context).GetAssetActivityAsync(
            asset.Id,
            new AssetActivityQuery { PageSize = 50 });

        Assert.Equal(AssetActivityResultStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(50, result.Response.Items.Count);
        Assert.True(result.Response.HasNextPage);
        Assert.All(result.Response.Items, item =>
        {
            Assert.Equal(1, item.InterventionCount);
            Assert.NotNull(item.HistoricalIntervention);
        });
        Assert.Equal(50, result.Response.Items.Select(item => item.WorkOrderId).Distinct().Count());
        Assert.Equal(4, commands.Commands.Count);
        var interventionCommand = Assert.Single(
            commands.Commands,
            command => command.Contains(
                "HistoricalInterventions",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains("WorkOrderId", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" IN ", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "AssetActivity.InterventionPage",
            interventionCommand,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Raw", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceFile", interventionCommand, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", interventionCommand, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cursor_pages_do_not_duplicate_or_skip_and_reject_invalid_cross_asset_and_stale_values()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var firstBatch = Batch("WorkOrder");
        var secondBatch = Batch("WorkOrder");
        var asset = Asset("A-1", "Main pump");
        var otherAsset = Asset("B-1", "Other pump");
        database.Context.AddRange(firstBatch, secondBatch, asset, otherAsset);
        var workOrders = new[]
        {
            WorkOrder("WO-1", new DateTime(2026, 8, 20), asset, firstBatch),
            WorkOrder("WO-2", new DateTime(2026, 8, 20), asset, firstBatch),
            WorkOrder("WO-3", new DateTime(2026, 8, 19), asset, firstBatch),
            WorkOrder("WO-4", null, asset, firstBatch),
            WorkOrder("WO-5", null, asset, firstBatch)
        };
        database.Context.WorkOrders.AddRange(workOrders);
        await database.Context.SaveChangesAsync();
        var expected = workOrders
            .OrderByDescending(item => item.ReportedDateTime)
            .ThenByDescending(item => item.Id)
            .Select(item => item.Id)
            .ToArray();
        var service = new EfAnalyticsQueryService(database.Context);

        var actual = new List<long>();
        string? cursor = null;
        do
        {
            var page = await service.GetAssetActivityAsync(
                asset.Id,
                new AssetActivityQuery { PageSize = 1, Cursor = cursor });
            Assert.Equal(AssetActivityResultStatus.Success, page.Status);
            Assert.NotNull(page.Response);
            Assert.InRange(page.Response.Items.Count, 0, 1);
            actual.AddRange(page.Response.Items.Select(item => item.WorkOrderId));
            cursor = page.Response.NextCursor;
            if (!page.Response.HasNextPage)
            {
                Assert.Null(cursor);
                break;
            }

            Assert.NotNull(cursor);
        }
        while (true);

        Assert.Equal(expected, actual);
        Assert.Equal(actual.Count, actual.Distinct().Count());

        var firstPage = await service.GetAssetActivityAsync(
            asset.Id,
            new AssetActivityQuery { PageSize = 1 });
        Assert.NotNull(firstPage.Response?.NextCursor);
        Assert.Equal(
            AssetActivityResultStatus.InvalidCursor,
            (await service.GetAssetActivityAsync(
                asset.Id,
                new AssetActivityQuery { Cursor = "not-a-valid-cursor" })).Status);
        Assert.Equal(
            AssetActivityResultStatus.InvalidCursor,
            (await service.GetAssetActivityAsync(
                otherAsset.Id,
                new AssetActivityQuery { Cursor = firstPage.Response.NextCursor })).Status);

        foreach (var workOrder in workOrders)
        {
            workOrder.LastSeenImportBatch = secondBatch;
        }
        await database.Context.SaveChangesAsync();
        Assert.Equal(
            AssetActivityResultStatus.StaleCursor,
            (await service.GetAssetActivityAsync(
                asset.Id,
                new AssetActivityQuery { Cursor = firstPage.Response.NextCursor })).Status);
    }

    [Fact]
    public async Task Activity_returns_empty_for_zero_history_not_found_for_unknown_and_propagates_cancellation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var emptyAsset = Asset("EMPTY", "Empty asset");
        database.Context.Assets.Add(emptyAsset);
        await database.Context.SaveChangesAsync();
        var service = new EfAnalyticsQueryService(database.Context);

        var empty = await service.GetAssetActivityAsync(emptyAsset.Id, new AssetActivityQuery());
        Assert.Equal(AssetActivityResultStatus.Success, empty.Status);
        Assert.NotNull(empty.Response);
        Assert.Equal(25, empty.Response.PageSize);
        Assert.Empty(empty.Response.Items);
        Assert.False(empty.Response.HasNextPage);
        Assert.Null(empty.Response.NextCursor);

        var missing = await service.GetAssetActivityAsync(long.MaxValue, new AssetActivityQuery());
        Assert.Equal(AssetActivityResultStatus.AssetNotFound, missing.Status);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetAssetActivityAsync(emptyAsset.Id, new AssetActivityQuery(), cancellation.Token));
    }

    [Fact]
    public async Task Asset_search_ranks_exact_prefix_and_name_matches_and_is_bounded_without_mutation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var building = new Building { Code = "B", Name = "Main Building" };
        var location = new Location { Name = "Pump Room", Building = building };
        var group = new AssetGroup { Code = "G", Name = "Pumps" };
        database.Context.Assets.AddRange(
            Asset("PUMP", "Exact code", building, location, group),
            Asset("PUMP-01", "Prefix one", building, location, group),
            Asset("PUMP-02", "Prefix two", building, location, group),
            Asset("ZZ-01", "Pump auxiliary", building, location, group));
        for (var index = 0; index < 25; index++)
        {
            database.Context.Assets.Add(Asset($"PUMP-{index + 100}", $"Pump {index}"));
        }
        await database.Context.SaveChangesAsync();
        var originalCount = database.Context.Assets.Count();
        var service = new EfAnalyticsQueryService(database.Context);

        var results = await service.SearchAssetsAsync(new AssetSearchQuery { Q = "  PUMP  " });
        Assert.Equal(10, results.Count);
        Assert.Equal("PUMP", results[0].AssetCode);
        Assert.Equal("PUMP-01", results[1].AssetCode);
        Assert.All(results.Skip(1), item => Assert.StartsWith("PUMP-", item.AssetCode));
        Assert.Equal(results.Count, results.Select(item => item.AssetId).Distinct().Count());
        Assert.All(results, item => Assert.True(item.AssetId > 0));

        var nameMatch = await service.SearchAssetsAsync(
            new AssetSearchQuery { Q = "auxiliary", Limit = 20 });
        Assert.Single(nameMatch);
        Assert.Equal("ZZ-01", nameMatch[0].AssetCode);
        Assert.Equal("Main Building", nameMatch[0].BuildingName);
        Assert.Equal("Pump Room", nameMatch[0].LocationName);
        Assert.Equal("Pumps", nameMatch[0].AssetGroupName);

        var maximum = await service.SearchAssetsAsync(
            new AssetSearchQuery { Q = "PUMP", Limit = 20 });
        Assert.Equal(20, maximum.Count);
        Assert.Empty(await service.SearchAssetsAsync(
            new AssetSearchQuery { Q = "NO-MATCH", Limit = 20 }));
        Assert.Equal(originalCount, database.Context.Assets.Count());

        var payload = JsonSerializer.Serialize(results);
        Assert.DoesNotContain("SerialNumber", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static ImportBatch Batch(string sourceType) =>
        new()
        {
            SourceType = sourceType,
            FileName = "test.xlsx",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Status = "Completed"
        };

    private static Asset Asset(
        string code,
        string name,
        Building? building = null,
        Location? location = null,
        AssetGroup? group = null) =>
        new()
        {
            AssetCode = code,
            Name = name,
            Building = building,
            Location = location,
            AssetGroup = group,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static WorkOrder WorkOrder(
        string number,
        DateTime? reportedAt,
        Asset? asset,
        ImportBatch snapshotBatch) =>
        new()
        {
            WorkOrderNumber = number,
            ReportedDateTime = reportedAt,
            Asset = asset,
            AssetCodeRaw = asset?.AssetCode ?? "UNLINKED",
            Description = $"Description {number}",
            Discipline = "MEKANÄ°K",
            Status = "KAPALI Ä°Å EMRÄ°",
            WorkType = "KONTROL",
            FailureType = "Ä°Å TALEBÄ°",
            RawStatusCode = WorkOrderSourceState.Closed,
            IsInCanonicalSnapshot = true,
            LastSeenImportBatch = snapshotBatch,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static HistoricalIntervention Intervention(
        WorkOrder workOrder,
        ImportBatch batch,
        HistoricalInterventionQuality quality,
        string? sanitizedAction,
        string rawAction,
        DateTime? completionDateTime,
        string fingerprint) =>
        new()
        {
            WorkOrder = workOrder,
            ImportBatch = batch,
            SourceYear = 2026,
            SourceWorkOrderNumber = workOrder.WorkOrderNumber,
            ReportedDateTime = workOrder.ReportedDateTime ?? new DateTime(2026, 1, 1),
            AssetCodeRaw = workOrder.AssetCodeRaw ?? "UNKNOWN",
            CompletionDateTime = completionDateTime,
            RequestDescriptionRaw = "RAW-REQUEST requester@example.com",
            RequestDescriptionSanitized = "Sanitized request",
            FailureReasonDescriptionRaw = "RAW-FAILURE",
            FailureReasonDescriptionSanitized = "Sanitized failure",
            WorkPerformedDescriptionRaw = rawAction,
            WorkPerformedDescriptionSanitized = sanitizedAction,
            InterventionQuality = quality,
            SourceRowFingerprint = fingerprint,
            FingerprintAlgorithm = "test/v1",
            SourceFileName = "private-source.xls",
            SourceSheet = "private-sheet",
            SourceRowNumber = 2,
            ImportedAt = DateTimeOffset.UtcNow
        };
}
