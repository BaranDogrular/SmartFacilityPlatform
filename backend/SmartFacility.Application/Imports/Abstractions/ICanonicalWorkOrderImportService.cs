using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface ICanonicalWorkOrderImportService
{
    Task<CanonicalWorkOrderPreflightResult> PreflightAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public interface ICanonicalWorkOrderSnapshotStore
{
    Task<CanonicalWorkOrderDatabasePreflight> PreflightAsync(
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken);

    Task<ImportResult> ApplyAsync(
        string sourceType,
        string fileName,
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken);
}
