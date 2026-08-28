using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Imports;

public sealed class EfCanonicalWorkOrderSnapshotStore(
    SmartFacilityDbContext dbContext,
    ILogger<EfCanonicalWorkOrderSnapshotStore> logger) : ICanonicalWorkOrderSnapshotStore
{
    private const string ImportLockResource = "SmartFacility:CanonicalWorkOrderSnapshot:v1";
    private const int SaveChunkSize = 500;

    public Task<CanonicalWorkOrderDatabasePreflight> PreflightAsync(
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken,
        CanonicalSnapshotImportOptions? options = null) =>
        InspectAsync(rows, options, cancellationToken);

    public async Task<ImportResult> ApplyAsync(
        string sourceType,
        string fileName,
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken,
        CanonicalSnapshotImportOptions? options = null)
    {
        var batch = new ImportBatch
        {
            SourceType = sourceType,
            FileName = fileName,
            StartedAt = DateTimeOffset.UtcNow,
            Status = "InProgress"
        };
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            await AcquireImportLockAsync(cancellationToken);
            var preflight = await InspectAsync(rows, options, cancellationToken);
            if (preflight.ExistingIdentityCollisions.Count > 0)
            {
                throw new InvalidOperationException(
                    "Canonical WorkOrder database preflight changed or failed after the import lock was acquired.");
            }

            if (!preflight.IsSnapshotCompletenessAllowed)
            {
                throw new CanonicalSnapshotSafetyException(
                    preflight.SafetyWarnings.FirstOrDefault()
                    ?? "Canonical snapshot completeness safety guard rejected the source.");
            }

            if (preflight.SuspiciousSnapshotShrinkOverrideApplied)
            {
                logger.LogWarning(
                    "Canonical snapshot shrink override applied. Source rows: {SourceRowCount}; " +
                    "current active: {CurrentActiveCount}; expected inactive: {ExpectedInactiveCount}; " +
                    "expected final active: {ExpectedFinalActiveCount}; shrink percent: {SourceShrinkPercent}.",
                    preflight.SourceRowCount,
                    preflight.CurrentActiveCount,
                    preflight.ExpectedInactiveCount,
                    preflight.ExpectedFinalActiveCount,
                    preflight.SourceShrinkPercent);
            }

            var assetMap = await dbContext.Assets
                .AsNoTracking()
                .ToDictionaryAsync(asset => asset.AssetCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var locationMap = await LoadUniqueLocationsAsync(cancellationToken);
            var existingMap = await LoadExistingMapAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var successfulRows = 0;
            var duplicateRows = 0;

            await dbContext.WorkOrders.ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IsInCanonicalSnapshot, false),
                cancellationToken);

            for (var index = 0; index < rows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[index];
                assetMap.TryGetValue(row.AssetCode, out var asset);
                locationMap.TryGetValue(row.LocationName ?? string.Empty, out var location);
                existingMap.TryGetValue(row.IdentityFingerprint, out var existing);
                var isDuplicate = existing is not null
                    && string.Equals(
                        existing.SourceRowFingerprint,
                        row.RowFingerprint,
                        StringComparison.Ordinal);

                var entity = CreateEntity(row, asset?.Id, location, existing, batch.Id, now);
                if (existing is null)
                {
                    dbContext.WorkOrders.Add(entity);
                }
                else
                {
                    dbContext.WorkOrders.Attach(entity);
                    dbContext.Entry(entity).State = EntityState.Modified;
                }

                dbContext.ImportSourceRecords.Add(new ImportSourceRecord
                {
                    ImportBatchId = batch.Id,
                    SourceSheet = row.SourceSheet,
                    SourceRowNumber = row.SourceRowNumber,
                    RowFingerprint = row.RowFingerprint,
                    IdempotencyFingerprint = row.IdentityFingerprint,
                    FingerprintAlgorithm = CanonicalWorkOrderIdentityCalculator.Algorithm,
                    RawData = row.RawData,
                    RawFormulaData = row.RawFormulaData,
                    ParseStatus = isDuplicate ? "Duplicate" : "Succeeded",
                    CreatedAt = now
                });

                if (isDuplicate)
                {
                    duplicateRows++;
                }
                else
                {
                    successfulRows++;
                }

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
            completedBatch.SuccessfulRows = successfulRows;
            completedBatch.FailedRows = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            return new ImportResult(
                batch.Id,
                "Completed",
                rows.Count,
                successfulRows,
                0,
                0,
                duplicateRows);
        }
        catch (Exception exception)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                logger.LogWarning(
                    rollbackException,
                    "Canonical WorkOrder transaction rollback failed after {ExceptionType}.",
                    exception.GetType().FullName);
            }

            dbContext.ChangeTracker.Clear();
            var failedBatch = await dbContext.ImportBatches
                .SingleAsync(item => item.Id == batch.Id, CancellationToken.None);
            failedBatch.Status = "Failed";
            failedBatch.CompletedAt = DateTimeOffset.UtcNow;
            failedBatch.TotalRows = rows.Count;
            failedBatch.SuccessfulRows = 0;
            failedBatch.FailedRows = 0;
            dbContext.ImportErrors.Add(new ImportError
            {
                ImportBatchId = batch.Id,
                ErrorMessage = exception switch
                {
                    OperationCanceledException =>
                        "Canonical WorkOrder import was cancelled; the snapshot transaction was rolled back.",
                    CanonicalSnapshotSafetyException => exception.Message,
                    _ => "Canonical WorkOrder import failed; the snapshot transaction was rolled back."
                }
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<CanonicalWorkOrderDatabasePreflight> InspectAsync(
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CanonicalSnapshotImportOptions? options,
        CancellationToken cancellationToken)
    {
        var assets = await dbContext.Assets
            .AsNoTracking()
            .Select(asset => new { asset.Id, asset.AssetCode })
            .ToListAsync(cancellationToken);
        var assetCodes = assets
            .Select(asset => asset.AssetCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unresolvedAssetCodes = rows
            .Select(row => row.AssetCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(code => !assetCodes.Contains(code))
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unresolvedAssetCodeSet = unresolvedAssetCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unresolvedAssetRowCount = rows.LongCount(row =>
            unresolvedAssetCodeSet.Contains(row.AssetCode));

        var locationCounts = await dbContext.Locations
            .AsNoTracking()
            .GroupBy(location => location.Name)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var ambiguousLocationNames = rows
            .Select(row => row.LocationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Join(
                locationCounts.Where(item => item.Count > 1),
                name => name,
                item => item.Name,
                (name, _) => name!,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ambiguousLocationNameSet = ambiguousLocationNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ambiguousLocationRowCount = rows.LongCount(row =>
            row.LocationName is not null && ambiguousLocationNameSet.Contains(row.LocationName));

        var existing = await LoadExistingRowsAsync(cancellationToken);
        var existingWithIdentity = existing
            .Select(item => new
            {
                Item = item,
                Identity = ResolveIdentity(item)
            })
            .Where(item => item.Identity is not null)
            .ToArray();
        var collisions = existingWithIdentity
            .GroupBy(item => item.Identity!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var existingByIdentity = existingWithIdentity
            .GroupBy(item => item.Identity!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.Ordinal);
        var incomingByIdentity = rows
            .GroupBy(row => row.IdentityFingerprint, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var matched = incomingByIdentity.LongCount(item => existingByIdentity.ContainsKey(item.Key));
        var expectedInsertCount = incomingByIdentity.Count - matched;
        var expectedReactivationCount = incomingByIdentity.LongCount(item =>
            existingByIdentity.TryGetValue(item.Key, out var existingItem)
            && !existingItem.IsInCanonicalSnapshot);
        var expectedUpdateCount = incomingByIdentity.LongCount(item =>
            existingByIdentity.TryGetValue(item.Key, out var existingItem)
            && !string.Equals(
                existingItem.SourceRowFingerprint,
                item.Value.RowFingerprint,
                StringComparison.Ordinal));
        var expectedUnchangedCount = matched - expectedUpdateCount;
        var activeIdentities = existingWithIdentity
            .Where(item => item.Item.IsInCanonicalSnapshot)
            .Select(item => item.Identity!)
            .ToHashSet(StringComparer.Ordinal);
        var matchedActiveCount = incomingByIdentity.LongCount(item => activeIdentities.Contains(item.Key));
        var currentActiveCount = existing.LongCount(item => item.IsInCanonicalSnapshot);
        var expectedInactiveCount = currentActiveCount - matchedActiveCount;
        var expectedFinalActiveCount = incomingByIdentity.Count;
        var importOptions = options ?? new CanonicalSnapshotImportOptions();
        var completeness = CanonicalSnapshotCompletenessGuard.Evaluate(
            currentActiveCount,
            rows.Count,
            expectedFinalActiveCount,
            expectedInactiveCount,
            importOptions.AllowSuspiciousSnapshotShrink);

        return new CanonicalWorkOrderDatabasePreflight(
            currentActiveCount,
            rows.Count,
            matched,
            expectedUnchangedCount,
            expectedInsertCount,
            expectedUpdateCount,
            expectedInactiveCount,
            expectedReactivationCount,
            expectedFinalActiveCount,
            completeness.SourceShrinkPercent,
            completeness.ExpectedInactivationPercent,
            completeness.Status,
            importOptions.AllowSuspiciousSnapshotShrink,
            completeness.OverrideApplied,
            completeness.IsAllowed,
            completeness.SafetyWarnings,
            unresolvedAssetRowCount,
            unresolvedAssetCodes,
            ambiguousLocationRowCount,
            ambiguousLocationNames,
            collisions);
    }

    private async Task<Dictionary<string, ExistingWorkOrder>> LoadExistingMapAsync(
        CancellationToken cancellationToken)
    {
        var existing = await LoadExistingRowsAsync(cancellationToken);
        var resolved = existing
            .Select(item => new { Item = item, Identity = ResolveIdentity(item) })
            .Where(item => item.Identity is not null)
            .ToArray();
        var collision = resolved
            .GroupBy(item => item.Identity!, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
        {
            throw new InvalidOperationException(
                $"Existing canonical identity collision: {collision.Key}.");
        }

        return resolved.ToDictionary(
            item => item.Identity!,
            item => item.Item,
            StringComparer.Ordinal);
    }

    private Task<List<ExistingWorkOrder>> LoadExistingRowsAsync(
        CancellationToken cancellationToken) =>
        dbContext.WorkOrders
            .AsNoTracking()
            .Select(item => new ExistingWorkOrder(
                item.Id,
                item.WorkOrderNumber,
                item.ReportedDateTime,
                item.AssetCodeRaw ?? (item.Asset == null ? null : item.Asset.AssetCode),
                item.CanonicalIdentityFingerprint,
                item.SourceRowFingerprint,
                item.CreatedAt,
                item.IsInCanonicalSnapshot))
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<string, LocationReference>> LoadUniqueLocationsAsync(
        CancellationToken cancellationToken)
    {
        var locations = await dbContext.Locations
            .AsNoTracking()
            .Select(location => new LocationReference(
                location.Name,
                location.Id,
                location.BuildingId))
            .ToListAsync(cancellationToken);

        return locations
            .GroupBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.OrdinalIgnoreCase);
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
                THROW 51000, 'Could not acquire the canonical WorkOrder import lock.', 1;
            """, cancellationToken);
    }

    private static string? ResolveIdentity(ExistingWorkOrder item)
    {
        if (!string.IsNullOrWhiteSpace(item.CanonicalIdentityFingerprint))
        {
            return item.CanonicalIdentityFingerprint;
        }

        return item.ReportedDateTime.HasValue && !string.IsNullOrWhiteSpace(item.AssetCode)
            ? CanonicalWorkOrderIdentityCalculator.Calculate(
                item.WorkOrderNumber,
                item.ReportedDateTime.Value,
                item.AssetCode)
            : null;
    }

    private static WorkOrder CreateEntity(
        CanonicalWorkOrderRow row,
        long? assetId,
        LocationReference? location,
        ExistingWorkOrder? existing,
        long batchId,
        DateTimeOffset now) =>
        new()
        {
            Id = existing?.Id ?? 0,
            WorkOrderNumber = row.WorkOrderNumber,
            ReportedDateTime = row.ReportedDateTime,
            AssetId = assetId,
            AssetCodeRaw = row.AssetCode,
            Description = row.Description,
            Discipline = row.Discipline,
            RequestedByName = row.RequestedByName,
            AssignedPersonnelName = row.AssignedPersonnelName,
            Status = row.Status,
            WorkType = row.WorkType,
            FailureType = row.FailureType,
            FailureReason = row.FailureReason,
            BuildingId = location?.BuildingId,
            LocationId = location?.LocationId,
            LocationNameRaw = row.LocationName,
            ResponseDurationRaw = row.ResponseDurationRaw,
            DowntimeRaw = row.DowntimeRaw,
            MaintenanceDurationRaw = row.MaintenanceDurationRaw,
            TotalCostRaw = row.TotalCostRaw,
            ServiceCostRaw = row.ServiceCostRaw,
            RawStatusCode = row.RawStatusCode,
            CanonicalIdentityFingerprint = row.IdentityFingerprint,
            SourceRowFingerprint = row.RowFingerprint,
            IsInCanonicalSnapshot = true,
            LastSeenImportBatchId = batchId,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };

    private sealed record ExistingWorkOrder(
        long Id,
        string WorkOrderNumber,
        DateTime? ReportedDateTime,
        string? AssetCode,
        string? CanonicalIdentityFingerprint,
        string? SourceRowFingerprint,
        DateTimeOffset CreatedAt,
        bool IsInCanonicalSnapshot);

    private sealed record LocationReference(string Name, long LocationId, long BuildingId);
}
