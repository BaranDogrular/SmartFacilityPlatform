using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class CanonicalWorkOrderSnapshotStoreTests
{
    [Fact]
    public async Task Same_snapshot_is_idempotent_and_work_order_number_is_not_unique_identity()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        var rows = new[]
        {
            Row(2, "WO-1", new DateTime(2026, 1, 1, 8, 0, 0), "first"),
            Row(3, "WO-1", new DateTime(2026, 1, 2, 8, 0, 0), "second")
        };

        var first = await store.ApplyAsync("WorkOrder", "snapshot.xlsx", rows, default);
        var second = await store.ApplyAsync("WorkOrder", "snapshot.xlsx", rows, default);

        Assert.Equal(2, first.SuccessfulRows);
        Assert.Equal(0, first.DuplicateRows);
        Assert.Equal(0, second.SuccessfulRows);
        Assert.Equal(2, second.DuplicateRows);
        Assert.Equal(2, await database.Context.WorkOrders.CountAsync());
        Assert.Equal(2, await database.Context.WorkOrders.CountAsync(item => item.IsInCanonicalSnapshot));
        Assert.Equal(4, await database.Context.ImportSourceRecords.CountAsync());
        Assert.Equal(2, await database.Context.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task Newer_snapshot_updates_matching_identity_adds_new_rows_and_retires_absent_rows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        database.Context.HistoricalWorkOrders.Add(new HistoricalWorkOrder
        {
            SourceReference = "LEGACY-SNAPSHOT",
            ReportedDateTime = new DateTime(2026, 1, 1),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        var store = CreateStore(database);
        var stableDate = new DateTime(2026, 1, 1, 8, 0, 0);

        await store.ApplyAsync(
            "WorkOrder",
            "first.xlsx",
            [Row(2, "WO-1", stableDate, "old"), Row(3, "WO-2", stableDate, "absent later")],
            default);
        var newer = await store.ApplyAsync(
            "WorkOrder",
            "newer.xlsx",
            [Row(2, "WO-1", stableDate, "updated"), Row(3, "WO-3", stableDate, "new")],
            default);

        Assert.Equal(2, newer.SuccessfulRows);
        Assert.Equal(3, await database.Context.WorkOrders.CountAsync());
        Assert.Equal(2, await database.Context.WorkOrders.CountAsync(item => item.IsInCanonicalSnapshot));
        Assert.Equal("updated", (await database.Context.WorkOrders.SingleAsync(
            item => item.WorkOrderNumber == "WO-1")).Description);
        Assert.False((await database.Context.WorkOrders.SingleAsync(
            item => item.WorkOrderNumber == "WO-2")).IsInCanonicalSnapshot);
        Assert.Equal(1, await database.Context.HistoricalWorkOrders.CountAsync());
    }

    [Fact]
    public async Task Preflight_reports_unresolved_assets_without_writing()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        var row = Row(2, "WO-1", new DateTime(2026, 1, 1), "test") with
        {
            AssetCode = "MISSING"
        };

        var result = await store.PreflightAsync([row], default);

        Assert.Equal(["MISSING"], result.UnresolvedAssetCodes);
        Assert.Equal(0, await database.Context.WorkOrders.CountAsync());
        Assert.Equal(0, await database.Context.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task Preflight_allows_full_snapshot_growth_and_reports_reconciliation_metadata()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        var initial = Rows(100);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", initial, default);
        var growing = Rows(105);

        var result = await store.PreflightAsync(growing, default);

        Assert.Equal(100, result.CurrentActiveCount);
        Assert.Equal(105, result.SourceRowCount);
        Assert.Equal(100, result.MatchedExistingCount);
        Assert.Equal(100, result.ExpectedUnchangedCount);
        Assert.Equal(5, result.ExpectedInsertCount);
        Assert.Equal(0, result.ExpectedUpdateCount);
        Assert.Equal(0, result.ExpectedInactiveCount);
        Assert.Equal(0, result.ExpectedReactivationCount);
        Assert.Equal(105, result.ExpectedFinalActiveCount);
        Assert.Equal(0m, result.SourceShrinkPercent);
        Assert.Equal(CanonicalSnapshotCompletenessGuard.CompleteStatus, result.SnapshotCompletenessStatus);
        Assert.True(result.IsSnapshotCompletenessAllowed);
        Assert.Empty(result.SafetyWarnings);
    }

    [Fact]
    public async Task Small_normal_shrink_is_allowed_and_percentages_are_correct()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", Rows(100), default);

        var result = await store.PreflightAsync(Rows(95), default);

        Assert.Equal(5, result.ExpectedInactiveCount);
        Assert.Equal(95, result.ExpectedFinalActiveCount);
        Assert.Equal(5m, result.SourceShrinkPercent);
        Assert.Equal(5m, result.ExpectedInactivationPercent);
        Assert.True(result.IsSnapshotCompletenessAllowed);
    }

    [Fact]
    public async Task Extreme_shrink_is_blocked_inside_apply_and_leaves_core_and_source_transaction_unchanged()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", Rows(200), default);
        var partial = Rows(50);

        var preflight = await store.PreflightAsync(partial, default);
        var exception = await Assert.ThrowsAsync<CanonicalSnapshotSafetyException>(() =>
            store.ApplyAsync("WorkOrder", "partial.xlsx", partial, default));

        Assert.Equal(150, preflight.ExpectedInactiveCount);
        Assert.Equal(75m, preflight.SourceShrinkPercent);
        Assert.Equal(CanonicalSnapshotCompletenessGuard.BlockedStatus, preflight.SnapshotCompletenessStatus);
        Assert.False(preflight.IsSnapshotCompletenessAllowed);
        Assert.Contains("50 rows", exception.Message, StringComparison.Ordinal);
        Assert.Contains("200 canonical records", exception.Message, StringComparison.Ordinal);
        Assert.Equal(200, await database.Context.WorkOrders.CountAsync(item => item.IsInCanonicalSnapshot));
        Assert.Equal(200, await database.Context.ImportSourceRecords.CountAsync());
        Assert.Equal(2, await database.Context.ImportBatches.CountAsync());
        Assert.Equal("Failed", (await database.Context.ImportBatches.OrderBy(item => item.Id).LastAsync()).Status);
        Assert.Contains(
            await database.Context.ImportErrors.ToListAsync(),
            error => error.ErrorMessage.Contains("75.00%", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Explicit_override_allows_suspicious_shrink_and_is_visible_in_preflight()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", Rows(200), default);
        var partial = Rows(50);
        var options = new CanonicalSnapshotImportOptions(AllowSuspiciousSnapshotShrink: true);

        var preflight = await store.PreflightAsync(partial, default, options);
        var result = await store.ApplyAsync("WorkOrder", "partial.xlsx", partial, default, options);

        Assert.True(preflight.IsSnapshotCompletenessAllowed);
        Assert.True(preflight.AllowSuspiciousSnapshotShrink);
        Assert.True(preflight.SuspiciousSnapshotShrinkOverrideApplied);
        Assert.Equal(CanonicalSnapshotCompletenessGuard.OverrideStatus, preflight.SnapshotCompletenessStatus);
        Assert.Single(preflight.SafetyWarnings);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(50, await database.Context.WorkOrders.CountAsync(item => item.IsInCanonicalSnapshot));
        Assert.Equal(150, await database.Context.WorkOrders.CountAsync(item => !item.IsInCanonicalSnapshot));
    }

    [Fact]
    public async Task Protected_recheck_blocks_a_preflight_that_became_stale()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", Rows(100), default);
        var staleSource = Rows(95);
        var originalPreflight = await store.PreflightAsync(staleSource, default);
        await store.ApplyAsync("WorkOrder", "concurrent.xlsx", Rows(200), default);
        var sourceRecordCountBeforeBlockedApply = await database.Context.ImportSourceRecords.CountAsync();

        await Assert.ThrowsAsync<CanonicalSnapshotSafetyException>(() =>
            store.ApplyAsync("WorkOrder", "stale.xlsx", staleSource, default));

        Assert.True(originalPreflight.IsSnapshotCompletenessAllowed);
        Assert.Equal(200, await database.Context.WorkOrders.CountAsync(item => item.IsInCanonicalSnapshot));
        Assert.Equal(
            sourceRecordCountBeforeBlockedApply,
            await database.Context.ImportSourceRecords.CountAsync());
        Assert.Equal("Failed", (await database.Context.ImportBatches.OrderBy(item => item.Id).LastAsync()).Status);
    }

    [Fact]
    public async Task Preflight_reports_updates_and_reactivations_separately()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        var initial = Rows(100);
        await store.ApplyAsync("WorkOrder", "initial.xlsx", initial, default);
        await store.ApplyAsync("WorkOrder", "smaller.xlsx", initial.Take(95).ToArray(), default);
        var restored = initial.ToArray();
        restored[0] = restored[0] with
        {
            Description = "updated",
            RowFingerprint = Fingerprint(restored[0].IdentityFingerprint, "updated")
        };

        var result = await store.PreflightAsync(restored, default);

        Assert.Equal(100, result.MatchedExistingCount);
        Assert.Equal(99, result.ExpectedUnchangedCount);
        Assert.Equal(1, result.ExpectedUpdateCount);
        Assert.Equal(5, result.ExpectedReactivationCount);
        Assert.Equal(0, result.ExpectedInactiveCount);
        Assert.Equal(100, result.ExpectedFinalActiveCount);
    }

    [Fact]
    public async Task Cancellation_after_snapshot_retirement_rolls_back_core_changes_and_records_failed_batch()
    {
        using var cancellationSource = new CancellationTokenSource();
        var interceptor = new CancelAfterSnapshotRetirementInterceptor(cancellationSource);
        await using var database = await SqliteTestDatabase.CreateAsync(interceptor);
        await SeedDimensionsAsync(database);
        var store = CreateStore(database);
        var reportedAt = new DateTime(2026, 1, 1, 8, 0, 0);
        await store.ApplyAsync(
            "WorkOrder",
            "first.xlsx",
            [Row(2, "WO-1", reportedAt, "original")],
            default);
        interceptor.Arm();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ApplyAsync(
            "WorkOrder",
            "cancelled.xlsx",
            [Row(2, "WO-1", reportedAt, "changed")],
            cancellationSource.Token));

        var workOrder = await database.Context.WorkOrders.SingleAsync();
        Assert.True(workOrder.IsInCanonicalSnapshot);
        Assert.Equal("original", workOrder.Description);
        Assert.Equal(2, await database.Context.ImportBatches.CountAsync());
        Assert.Equal("Failed", (await database.Context.ImportBatches.OrderBy(item => item.Id).LastAsync()).Status);
        Assert.Contains(
            await database.Context.ImportErrors.ToListAsync(),
            error => error.ErrorMessage.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    private static EfCanonicalWorkOrderSnapshotStore CreateStore(SqliteTestDatabase database) =>
        new(database.Context, NullLogger<EfCanonicalWorkOrderSnapshotStore>.Instance);

    private static async Task SeedDimensionsAsync(SqliteTestDatabase database)
    {
        var building = new Building { Code = "B", Name = "Building" };
        var location = new Location { Name = "Location", Building = building };
        database.Context.Assets.Add(new Asset
        {
            AssetCode = "ASSET-1",
            Name = "Asset",
            Building = building,
            Location = location,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
    }

    private static CanonicalWorkOrderRow Row(
        int rowNumber,
        string number,
        DateTime reportedAt,
        string description)
    {
        var identity = CanonicalWorkOrderIdentityCalculator.Calculate(
            number,
            reportedAt,
            "ASSET-1");
        var rowFingerprint = Fingerprint(identity, description);

        return new CanonicalWorkOrderRow(
            "İş Emirleri",
            rowNumber,
            rowFingerprint,
            identity,
            "{}",
            null,
            number,
            reportedAt,
            "ASSET-1",
            description,
            "Electrical",
            null,
            null,
            "Kapalı",
            "Corrective",
            null,
            null,
            "Location",
            null,
            null,
            null,
            null,
            null,
            "K");
    }

    private static CanonicalWorkOrderRow[] Rows(int count) =>
        Enumerable.Range(1, count)
            .Select(index => Row(
                index + 1,
                $"WO-{index}",
                new DateTime(2026, 1, 1, 8, 0, 0).AddMinutes(index),
                $"description-{index}"))
            .ToArray();

    private static string Fingerprint(string identity, string description) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{identity}|{description}")));

    private sealed class CancelAfterSnapshotRetirementInterceptor(
        CancellationTokenSource cancellationSource) : DbCommandInterceptor
    {
        private bool _armed;

        public void Arm() => _armed = true;

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (_armed
                && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("WorkOrders", StringComparison.OrdinalIgnoreCase))
            {
                _armed = false;
                cancellationSource.Cancel();
            }

            return ValueTask.FromResult(result);
        }
    }
}
