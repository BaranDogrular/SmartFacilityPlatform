using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Imports;

public sealed class EfHistoricalInterventionStore(
    SmartFacilityDbContext dbContext,
    ILogger<EfHistoricalInterventionStore> logger) : IHistoricalInterventionStore
{
    private const string SourceType = "HistoricalIntervention";
    private const string ImportLockResource = "SmartFacility:HistoricalIntervention:v1";
    private const int SaveChunkSize = 500;

    public Task<HistoricalInterventionDatabasePreflight> PreflightAsync(
        IReadOnlyList<HistoricalInterventionImportRow> rows,
        CancellationToken cancellationToken) => InspectAsync(rows, cancellationToken);

    public async Task<ImportResult> ApplyAsync(
        IReadOnlyList<HistoricalInterventionImportRow> rows,
        IReadOnlyList<HistoricalInterventionSourceFileSummary> files,
        CancellationToken cancellationToken)
    {
        var batch = new ImportBatch
        {
            SourceType = SourceType,
            FileName = string.Join(" | ", files.Select(file => file.FileName)),
            StartedAt = DateTimeOffset.UtcNow,
            Status = "InProgress"
        };
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await AcquireImportLockAsync(cancellationToken);
            if (!await HistoricalInterventionTableExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "The Historical Intervention schema migration has not been applied.");
            }

            var protectedPreflight = await InspectAsync(rows, cancellationToken);
            if (protectedPreflight.UnmatchedRows != 0
                || protectedPreflight.AmbiguousRows != 0
                || protectedPreflight.StrictCanonicalMatches != rows.Count)
            {
                throw new InvalidOperationException(
                    "Canonical linkage changed after the Historical Intervention import lock was acquired.");
            }

            var workOrders = await LoadUniqueCanonicalMapAsync(cancellationToken);
            var existingFingerprints = await dbContext.HistoricalInterventions
                .AsNoTracking()
                .Select(item => item.SourceRowFingerprint)
                .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var inserted = 0;
            var duplicates = 0;

            for (var index = 0; index < rows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[index];
                var duplicate = existingFingerprints.Contains(row.SourceRowFingerprint);
                if (!duplicate)
                {
                    var workOrder = workOrders[row.CanonicalIdentityFingerprint];
                    dbContext.HistoricalInterventions.Add(CreateEntity(row, workOrder.Id, batch.Id, now));
                    existingFingerprints.Add(row.SourceRowFingerprint);
                    inserted++;
                }
                else
                {
                    duplicates++;
                }

                dbContext.ImportSourceRecords.Add(new ImportSourceRecord
                {
                    ImportBatchId = batch.Id,
                    SourceSheet = row.Source.SourceSheet,
                    SourceRowNumber = row.Source.SourceRowNumber,
                    RowFingerprint = row.SourceRowFingerprint,
                    IdempotencyFingerprint = row.SourceRowFingerprint,
                    FingerprintAlgorithm = HistoricalInterventionFingerprintCalculator.Algorithm,
                    RawData = row.AuditRawData,
                    ParseStatus = duplicate ? "Duplicate" : "Succeeded",
                    CreatedAt = now
                });

                if ((index + 1) % SaveChunkSize == 0)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var completedBatch = await dbContext.ImportBatches
                .SingleAsync(item => item.Id == batch.Id, cancellationToken);
            completedBatch.Status = "Completed";
            completedBatch.CompletedAt = DateTimeOffset.UtcNow;
            completedBatch.TotalRows = rows.Count;
            completedBatch.SuccessfulRows = inserted;
            completedBatch.FailedRows = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            return new ImportResult(
                batch.Id,
                "Completed",
                rows.Count,
                inserted,
                0,
                0,
                duplicates);
        }
        catch (Exception exception)
        {
            try
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
            catch (Exception rollbackException)
            {
                logger.LogWarning(
                    rollbackException,
                    "Historical Intervention rollback failed after {ExceptionType}.",
                    exception.GetType().FullName);
            }

            dbContext.ChangeTracker.Clear();
            var failedBatch = await dbContext.ImportBatches
                .SingleAsync(item => item.Id == batch.Id, CancellationToken.None);
            failedBatch.Status = "Failed";
            failedBatch.CompletedAt = DateTimeOffset.UtcNow;
            failedBatch.TotalRows = rows.Count;
            failedBatch.SuccessfulRows = 0;
            failedBatch.FailedRows = rows.Count;
            dbContext.ImportErrors.Add(new ImportError
            {
                ImportBatchId = batch.Id,
                ErrorMessage = exception is OperationCanceledException
                    ? "Historical Intervention import was cancelled; all intervention and lineage writes were rolled back."
                    : "Historical Intervention import failed; all intervention and lineage writes were rolled back."
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<HistoricalInterventionDatabasePreflight> InspectAsync(
        IReadOnlyList<HistoricalInterventionImportRow> rows,
        CancellationToken cancellationToken)
    {
        var canonicalRows = await LoadCanonicalRowsAsync(cancellationToken);
        var groups = canonicalRows
            .Where(item => item.Identity is not null)
            .GroupBy(item => item.Identity!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        long matched = 0;
        long active = 0;
        long inactive = 0;
        long unmatched = 0;
        long ambiguous = 0;
        var unmatchedReferences = new List<string>();
        var ambiguousReferences = new List<string>();

        foreach (var row in rows)
        {
            if (!groups.TryGetValue(row.CanonicalIdentityFingerprint, out var candidates))
            {
                unmatched++;
                AddSafeReference(unmatchedReferences, row);
            }
            else if (candidates.Length != 1)
            {
                ambiguous++;
                AddSafeReference(ambiguousReferences, row);
            }
            else
            {
                matched++;
                if (candidates[0].IsActive)
                {
                    active++;
                }
                else
                {
                    inactive++;
                }
            }
        }

        var schemaExists = await HistoricalInterventionTableExistsAsync(cancellationToken);
        var existingFingerprints = schemaExists
            ? await dbContext.HistoricalInterventions
                .AsNoTracking()
                .Select(item => item.SourceRowFingerprint)
                .ToHashSetAsync(StringComparer.Ordinal, cancellationToken)
            : new HashSet<string>(StringComparer.Ordinal);
        var unchanged = rows.LongCount(row => existingFingerprints.Contains(row.SourceRowFingerprint));

        return new HistoricalInterventionDatabasePreflight(
            schemaExists,
            matched,
            active,
            inactive,
            unmatched,
            ambiguous,
            existingFingerprints.Count,
            rows.Count - unchanged,
            unchanged,
            unmatchedReferences,
            ambiguousReferences);
    }

    private async Task<bool> HistoricalInterventionTableExistsAsync(
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return true;
        }

        var result = await dbContext.Database
            .SqlQueryRaw<int>(
                "SELECT CASE WHEN OBJECT_ID(N'[core].[HistoricalInterventions]', N'U') " +
                "IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(cancellationToken);
        return result == 1;
    }

    private async Task<Dictionary<string, CanonicalReference>> LoadUniqueCanonicalMapAsync(
        CancellationToken cancellationToken)
    {
        var rows = await LoadCanonicalRowsAsync(cancellationToken);
        var groups = rows
            .Where(item => item.Identity is not null)
            .GroupBy(item => item.Identity!, StringComparer.Ordinal)
            .ToArray();
        if (groups.Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "The canonical WorkOrder table contains an identity collision.");
        }

        return groups.ToDictionary(
            group => group.Key,
            group => group.Single(),
            StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<CanonicalReference>> LoadCanonicalRowsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.WorkOrders
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.WorkOrderNumber,
                item.ReportedDateTime,
                AssetCode = item.AssetCodeRaw ?? (item.Asset == null ? null : item.Asset.AssetCode),
                item.CanonicalIdentityFingerprint,
                item.IsInCanonicalSnapshot
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new CanonicalReference(
                item.Id,
                ResolveIdentity(
                    item.WorkOrderNumber,
                    item.ReportedDateTime,
                    item.AssetCode,
                    item.CanonicalIdentityFingerprint),
                item.IsInCanonicalSnapshot))
            .ToArray();
    }

    private async Task AcquireImportLockAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {ImportLockResource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 180000;
            IF @result < 0
                THROW 51000, 'Could not acquire the Historical Intervention import lock.', 1;
            """, cancellationToken);
    }

    private static string? ResolveIdentity(
        string workOrderNumber,
        DateTime? reportedAt,
        string? assetCode,
        string? existingFingerprint)
    {
        if (!string.IsNullOrWhiteSpace(existingFingerprint))
        {
            return existingFingerprint;
        }

        return reportedAt.HasValue && !string.IsNullOrWhiteSpace(assetCode)
            ? CanonicalWorkOrderIdentityCalculator.Calculate(workOrderNumber, reportedAt.Value, assetCode)
            : null;
    }

    private static void AddSafeReference(
        ICollection<string> references,
        HistoricalInterventionImportRow row)
    {
        if (references.Count < 20)
        {
            var safeIdentity = row.CanonicalIdentityFingerprint[..Math.Min(
                12,
                row.CanonicalIdentityFingerprint.Length)];
            references.Add(
                $"{row.Source.SourceFileName}/{row.Source.SourceSheet}!{row.Source.SourceRowNumber} " +
                $"(identity {safeIdentity})");
        }
    }

    private static HistoricalIntervention CreateEntity(
        HistoricalInterventionImportRow row,
        long workOrderId,
        long batchId,
        DateTimeOffset now) =>
        new()
        {
            WorkOrderId = workOrderId,
            ImportBatchId = batchId,
            SourceYear = row.Source.SourceYear,
            SourceWorkOrderNumber = row.Source.WorkOrderNumber,
            ReportedDateTime = row.Source.ReportedDateTime,
            AssetCodeRaw = row.Source.AssetCode,
            WorkOrderStatus = row.Source.WorkOrderStatus,
            AssetName = row.Source.AssetName,
            CompletionDateTime = row.Source.CompletionDateTime,
            RequestDescriptionRaw = row.Source.RequestDescription,
            RequestDescriptionSanitized = row.RequestDescriptionSanitized,
            WorkPerformedDescriptionRaw = row.Source.WorkPerformedDescription,
            WorkPerformedDescriptionSanitized = row.WorkPerformedDescriptionSanitized,
            FailureReasonCode = row.Source.FailureReasonCode,
            FailureReasonDescriptionRaw = row.Source.FailureReasonDescription,
            FailureReasonDescriptionSanitized = row.FailureReasonDescriptionSanitized,
            MaintenanceDurationRaw = row.Source.MaintenanceDurationRaw,
            DowntimeDurationRaw = row.Source.DowntimeDurationRaw,
            LaborDurationRaw = row.Source.LaborDurationRaw,
            MaterialCostRaw = row.Source.MaterialCostRaw,
            LaborCostRaw = row.Source.LaborCostRaw,
            TotalCostRaw = row.Source.TotalCostRaw,
            TotalCostCurrencyRaw = row.Source.TotalCostCurrencyRaw,
            InterventionQuality = row.InterventionQuality,
            SourceRowFingerprint = row.SourceRowFingerprint,
            FingerprintAlgorithm = HistoricalInterventionFingerprintCalculator.Algorithm,
            SourceFileName = row.Source.SourceFileName,
            SourceSheet = row.Source.SourceSheet,
            SourceRowNumber = row.Source.SourceRowNumber,
            ImportedAt = now
        };

    private sealed record CanonicalReference(long Id, string? Identity, bool IsActive);
}
