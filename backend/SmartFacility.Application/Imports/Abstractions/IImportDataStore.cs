using SmartFacility.Application.Imports.Models;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportDataStore
{
    Task<ImportBatch> CreateBatchAsync(
        string sourceType,
        string fileName,
        CancellationToken cancellationToken);

    Task CompleteBatchAsync(
        long batchId,
        string status,
        int totalRows,
        int successfulRows,
        int failedRows,
        CancellationToken cancellationToken);

    Task RecordBatchFailureAsync(
        long batchId,
        string errorMessage,
        int totalRows,
        int successfulRows,
        int failedRows,
        CancellationToken cancellationToken);

    Task<ISet<string>> GetSuccessfulFingerprintsAsync(
        string sourceType,
        IReadOnlyCollection<string> sheetNames,
        string? fingerprintAlgorithm,
        CancellationToken cancellationToken);

    Task ExecuteRowAsync(
        ImportSourceRecord sourceRecord,
        Func<CancellationToken, Task<ImportRowDecision>> operation,
        CancellationToken cancellationToken);

    Task<Asset?> FindAssetByCodeAsync(string assetCode, CancellationToken cancellationToken);
    Task<Building> GetOrAddBuildingAsync(string? code, string name, CancellationToken cancellationToken);
    Task<Location> GetOrAddLocationAsync(Building building, string name, CancellationToken cancellationToken);
    Task<AssetGroup> GetOrAddAssetGroupAsync(string? code, string name, CancellationToken cancellationToken);
    Task<Location?> FindUniqueLocationByNameAsync(string name, CancellationToken cancellationToken);
}
