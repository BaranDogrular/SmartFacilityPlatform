using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class HistoricalInterventionStoreTests
{
    [Fact]
    public async Task Strict_linkage_includes_inactive_canonical_rows_and_same_import_is_idempotent()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var canonical = await SeedCanonicalAsync(database, isActive: false);
        var row = Row(canonical.CanonicalIdentityFingerprint!);
        var store = new EfHistoricalInterventionStore(
            database.Context,
            NullLogger<EfHistoricalInterventionStore>.Instance);

        var preflight = await store.PreflightAsync([row], default);
        var first = await store.ApplyAsync([row], [FileSummary()], default);
        var second = await store.ApplyAsync([row], [FileSummary()], default);

        Assert.Equal(1, preflight.StrictCanonicalMatches);
        Assert.Equal(0, preflight.ActiveCanonicalMatches);
        Assert.Equal(1, preflight.InactiveCanonicalMatches);
        Assert.Equal(1, first.SuccessfulRows);
        Assert.Equal(0, first.DuplicateRows);
        Assert.Equal(0, second.SuccessfulRows);
        Assert.Equal(1, second.DuplicateRows);
        Assert.Equal(1, await database.Context.HistoricalInterventions.CountAsync());
        Assert.Equal(2, await database.Context.ImportSourceRecords.CountAsync());
        Assert.Equal(2, await database.Context.ImportBatches.CountAsync());
        Assert.All(
            await database.Context.ImportBatches.ToListAsync(),
            batch => Assert.Equal("Completed", batch.Status));
    }

    [Fact]
    public async Task Missing_strict_identity_is_reported_without_writes()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var store = new EfHistoricalInterventionStore(
            database.Context,
            NullLogger<EfHistoricalInterventionStore>.Instance);

        var preflight = await store.PreflightAsync([Row("MISSING")], default);

        Assert.Equal(0, preflight.StrictCanonicalMatches);
        Assert.Equal(1, preflight.UnmatchedRows);
        Assert.Single(preflight.UnmatchedReferences);
        Assert.Equal(0, await database.Context.HistoricalInterventions.CountAsync());
        Assert.Equal(0, await database.Context.ImportBatches.CountAsync());
    }

    [Fact]
    public async Task Cancellation_rolls_back_intervention_and_lineage_and_keeps_failed_batch_audit()
    {
        using var cancellationSource = new CancellationTokenSource();
        var interceptor = new CancelInterventionInsertInterceptor(cancellationSource);
        await using var database = await SqliteTestDatabase.CreateAsync(interceptor);
        var canonical = await SeedCanonicalAsync(database, isActive: true);
        var store = new EfHistoricalInterventionStore(
            database.Context,
            NullLogger<EfHistoricalInterventionStore>.Instance);
        interceptor.Arm();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ApplyAsync(
            [Row(canonical.CanonicalIdentityFingerprint!)],
            [FileSummary()],
            cancellationSource.Token));

        Assert.Equal(0, await database.Context.HistoricalInterventions.CountAsync());
        Assert.Equal(0, await database.Context.ImportSourceRecords.CountAsync());
        var batch = await database.Context.ImportBatches.SingleAsync();
        Assert.Equal("Failed", batch.Status);
        Assert.Equal(1, await database.Context.ImportErrors.CountAsync());
    }

    private static async Task<WorkOrder> SeedCanonicalAsync(
        SqliteTestDatabase database,
        bool isActive)
    {
        var reportedAt = new DateTime(2026, 1, 1, 8, 0, 0);
        var identity = CanonicalWorkOrderIdentityCalculator.Calculate("WO-1", reportedAt, "ASSET-1");
        var workOrder = new WorkOrder
        {
            WorkOrderNumber = "WO-1",
            ReportedDateTime = reportedAt,
            AssetCodeRaw = "ASSET-1",
            CanonicalIdentityFingerprint = identity,
            SourceRowFingerprint = "SOURCE",
            IsInCanonicalSnapshot = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        database.Context.WorkOrders.Add(workOrder);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        return workOrder;
    }

    private static HistoricalInterventionImportRow Row(string identity)
    {
        var source = new HistoricalInterventionSourceRow(
            "source.xls",
            "source.xls",
            "HASH",
            "Varlık Tarihçesi",
            2,
            2026,
            "WO-1",
            new DateTime(2026, 1, 1, 8, 0, 0),
            "ASSET-1",
            "K",
            "Asset",
            new DateTime(2026, 1, 1, 9, 0, 0),
            "Problem",
            "Filtre değiştirilerek sistem test edildi.",
            "R1",
            "Reason",
            "1",
            "0",
            "1",
            "10",
            "20",
            "30",
            "TRY 30");
        var fingerprint = HistoricalInterventionFingerprintCalculator.Calculate(source, identity);
        return new HistoricalInterventionImportRow(
            source,
            identity,
            fingerprint,
            HistoricalInterventionQuality.Informative,
            "Problem",
            "Filtre değiştirilerek sistem test edildi.",
            "Reason",
            "{}");
    }

    private static HistoricalInterventionSourceFileSummary FileSummary() =>
        new(
            "source.xls",
            "source.xls",
            "HASH",
            1,
            "Varlık Tarihçesi",
            2,
            1,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1));

    private sealed class CancelInterventionInsertInterceptor(
        CancellationTokenSource cancellationSource) : DbCommandInterceptor
    {
        private bool _armed;

        public void Arm() => _armed = true;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed
                && command.CommandText.Contains(
                    "HistoricalInterventions",
                    StringComparison.OrdinalIgnoreCase))
            {
                _armed = false;
                cancellationSource.Cancel();
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed
                && command.CommandText.Contains(
                    "HistoricalInterventions",
                    StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                _armed = false;
                cancellationSource.Cancel();
            }

            return ValueTask.FromResult(result);
        }
    }
}
