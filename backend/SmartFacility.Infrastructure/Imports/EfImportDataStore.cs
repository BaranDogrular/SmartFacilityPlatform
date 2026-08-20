using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Persistence;

namespace SmartFacility.Infrastructure.Imports;

public sealed class EfImportDataStore(
    SmartFacilityDbContext dbContext,
    IImportIdempotencyLock idempotencyLock,
    IImportDimensionLock dimensionLock,
    ILogger<EfImportDataStore> logger) : IImportDataStore
{
    private List<IAsyncDisposable>? _activeDimensionLockLeases;

    public async Task<ImportBatch> CreateBatchAsync(
        string sourceType,
        string fileName,
        CancellationToken cancellationToken)
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
        return batch;
    }

    public async Task CompleteBatchAsync(
        long batchId,
        string status,
        int totalRows,
        int successfulRows,
        int failedRows,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .SingleAsync(item => item.Id == batchId, cancellationToken);

        batch.Status = status;
        batch.CompletedAt = DateTimeOffset.UtcNow;
        batch.TotalRows = totalRows;
        batch.SuccessfulRows = successfulRows;
        batch.FailedRows = failedRows;

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task RecordBatchFailureAsync(
        long batchId,
        string errorMessage,
        int totalRows,
        int successfulRows,
        int failedRows,
        CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .SingleAsync(item => item.Id == batchId, cancellationToken);

        batch.Status = "Failed";
        batch.CompletedAt = DateTimeOffset.UtcNow;
        batch.TotalRows = totalRows;
        batch.SuccessfulRows = successfulRows;
        batch.FailedRows = failedRows;

        dbContext.ImportErrors.Add(new ImportError
        {
            ImportBatchId = batchId,
            ErrorMessage = errorMessage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task<ISet<string>> GetSuccessfulFingerprintsAsync(
        string sourceType,
        IReadOnlyCollection<string> sheetNames,
        string? fingerprintAlgorithm,
        CancellationToken cancellationToken)
    {
        var records = dbContext.ImportSourceRecords
            .AsNoTracking()
            .Where(record =>
                record.ImportBatch.SourceType == sourceType &&
                sheetNames.Contains(record.SourceSheet) &&
                (record.ParseStatus == "Succeeded" || record.ParseStatus == "Duplicate"));

        var fingerprints = fingerprintAlgorithm is null
            ? await records
                .Select(record => record.RowFingerprint)
                .ToListAsync(cancellationToken)
            : await records
                .Where(record =>
                    record.FingerprintAlgorithm == fingerprintAlgorithm &&
                    record.IdempotencyFingerprint != null)
                .Select(record => record.IdempotencyFingerprint!)
                .ToListAsync(cancellationToken);

        return fingerprints.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<ImportRowDecision> ExecuteRowAsync(
        string sourceType,
        ImportSourceRecord sourceRecord,
        bool enforceIdempotency,
        Func<CancellationToken, Task<ImportRowDecision>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        IAsyncDisposable? idempotencyLease = null;
        _activeDimensionLockLeases = [];

        try
        {
            ImportRowDecision decision;
            if (enforceIdempotency)
            {
                var duplicateFingerprint = sourceRecord.IdempotencyFingerprint
                    ?? sourceRecord.RowFingerprint;
                idempotencyLease = await idempotencyLock.AcquireAsync(
                    sourceType,
                    sourceRecord.SourceSheet,
                    sourceRecord.FingerprintAlgorithm,
                    duplicateFingerprint,
                    cancellationToken);
                decision = await HasSuccessfulFingerprintAsync(
                    sourceType,
                    sourceRecord,
                    cancellationToken)
                    ? ImportRowDecision.Duplicate()
                    : await operation(cancellationToken);
            }
            else
            {
                decision = await operation(cancellationToken);
            }

            sourceRecord.ParseStatus = decision.Disposition switch
            {
                ImportRowDisposition.Success => "Succeeded",
                ImportRowDisposition.Error => "Failed",
                ImportRowDisposition.Ignore => "Ignored",
                ImportRowDisposition.Duplicate => "Duplicate",
                _ => throw new ArgumentOutOfRangeException(nameof(decision))
            };

            dbContext.ImportSourceRecords.Add(sourceRecord);

            if (decision.Disposition == ImportRowDisposition.Error)
            {
                dbContext.ImportErrors.Add(new ImportError
                {
                    ImportBatchId = sourceRecord.ImportBatchId,
                    RowNumber = sourceRecord.SourceRowNumber,
                    ErrorMessage = decision.ErrorMessage ?? "The row failed validation.",
                    RawData = sourceRecord.RawData
                });
            }
            else if (decision.Disposition == ImportRowDisposition.Success)
            {
                AddEntity(decision.Entity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return decision;
        }
        catch (Exception originalException)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                logger.LogWarning(
                    rollbackException,
                    "Import row transaction rollback failed after {OriginalExceptionType}. " +
                    "The original exception will be rethrown.",
                    originalException.GetType().FullName);
            }

            throw;
        }
        finally
        {
            if (idempotencyLease is not null)
            {
                await idempotencyLease.DisposeAsync();
            }

            var dimensionLockLeases = _activeDimensionLockLeases;
            _activeDimensionLockLeases = null;
            if (dimensionLockLeases is not null)
            {
                for (var index = dimensionLockLeases.Count - 1; index >= 0; index--)
                {
                    await dimensionLockLeases[index].DisposeAsync();
                }
            }

            dbContext.ChangeTracker.Clear();
        }
    }

    private Task<bool> HasSuccessfulFingerprintAsync(
        string sourceType,
        ImportSourceRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var records = dbContext.ImportSourceRecords
            .AsNoTracking()
            .Where(record =>
                record.ImportBatch.SourceType == sourceType &&
                record.SourceSheet == sourceRecord.SourceSheet &&
                (record.ParseStatus == "Succeeded" || record.ParseStatus == "Duplicate"));

        return sourceRecord.FingerprintAlgorithm is null
            ? records.AnyAsync(
                record => record.RowFingerprint == sourceRecord.RowFingerprint,
                cancellationToken)
            : records.AnyAsync(
                record =>
                    record.FingerprintAlgorithm == sourceRecord.FingerprintAlgorithm &&
                    record.IdempotencyFingerprint == sourceRecord.IdempotencyFingerprint,
                cancellationToken);
    }

    public Task<Asset?> FindAssetByCodeAsync(string assetCode, CancellationToken cancellationToken) =>
        dbContext.Assets.SingleOrDefaultAsync(
            asset => asset.AssetCode == assetCode,
            cancellationToken);

    public async Task<Building> GetOrAddBuildingAsync(
        string? code,
        string name,
        CancellationToken cancellationToken)
    {
        await AcquireDimensionLockAsync(
            "Building",
            code,
            name,
            identityPart3: null,
            cancellationToken);

        var building = await dbContext.Buildings.FirstOrDefaultAsync(
            item => item.Code == code && item.Name == name,
            cancellationToken);

        if (building is not null)
        {
            return building;
        }

        building = new Building { Code = code, Name = name };
        dbContext.Buildings.Add(building);
        return building;
    }

    public async Task<Location> GetOrAddLocationAsync(
        Building building,
        string name,
        CancellationToken cancellationToken)
    {
        await AcquireDimensionLockAsync(
            building.Id == 0 ? "LocationForNewBuilding" : "Location",
            building.Id == 0
                ? building.Code
                : building.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            building.Id == 0 ? building.Name : name,
            building.Id == 0 ? name : null,
            cancellationToken);

        if (building.Id != 0)
        {
            var location = await dbContext.Locations.FirstOrDefaultAsync(
                item => item.BuildingId == building.Id && item.Name == name,
                cancellationToken);

            if (location is not null)
            {
                return location;
            }
        }

        var newLocation = new Location { Building = building, Name = name };
        dbContext.Locations.Add(newLocation);
        return newLocation;
    }

    public async Task<AssetGroup> GetOrAddAssetGroupAsync(
        string? code,
        string name,
        CancellationToken cancellationToken)
    {
        await AcquireDimensionLockAsync(
            "AssetGroup",
            code,
            name,
            identityPart3: null,
            cancellationToken);

        var group = await dbContext.AssetGroups.FirstOrDefaultAsync(
            item => item.Code == code && item.Name == name,
            cancellationToken);

        if (group is not null)
        {
            return group;
        }

        group = new AssetGroup { Code = code, Name = name };
        dbContext.AssetGroups.Add(group);
        return group;
    }

    private async Task AcquireDimensionLockAsync(
        string dimensionName,
        string? identityPart1,
        string? identityPart2,
        string? identityPart3,
        CancellationToken cancellationToken)
    {
        var leases = _activeDimensionLockLeases
            ?? throw new InvalidOperationException(
                "An active row transaction is required before acquiring an import dimension lock.");
        var lease = await dimensionLock.AcquireAsync(
            dimensionName,
            identityPart1,
            identityPart2,
            identityPart3,
            cancellationToken);
        leases.Add(lease);
    }

    public async Task<Location?> FindUniqueLocationByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var matches = await dbContext.Locations
            .Include(location => location.Building)
            .Where(location => location.Name == name)
            .Take(2)
            .ToListAsync(cancellationToken);

        return matches.Count == 1 ? matches[0] : null;
    }

    private void AddEntity(object? entity)
    {
        switch (entity)
        {
            case Asset asset when asset.Id == 0:
                dbContext.Assets.Add(asset);
                break;
            case Asset:
                break;
            case WorkOrder workOrder:
                dbContext.WorkOrders.Add(workOrder);
                break;
            case HistoricalWorkOrder historicalWorkOrder:
                dbContext.HistoricalWorkOrders.Add(historicalWorkOrder);
                break;
            case ScadaAlarmEvent alarmEvent:
                dbContext.ScadaAlarmEvents.Add(alarmEvent);
                break;
            case ScadaOutage outage:
                dbContext.ScadaOutages.Add(outage);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported import entity type: {entity?.GetType().Name ?? "null"}.");
        }
    }
}
