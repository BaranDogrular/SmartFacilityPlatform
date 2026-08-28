using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface ICanonicalWorkOrderImportService
{
    Task<CanonicalWorkOrderPreflightResult> PreflightAsync(
        string filePath,
        CancellationToken cancellationToken = default,
        CanonicalSnapshotImportOptions? options = null);

    Task<ImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default,
        CanonicalSnapshotImportOptions? options = null);
}

public interface ICanonicalWorkOrderSnapshotStore
{
    Task<CanonicalWorkOrderDatabasePreflight> PreflightAsync(
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken,
        CanonicalSnapshotImportOptions? options = null);

    Task<ImportResult> ApplyAsync(
        string sourceType,
        string fileName,
        IReadOnlyList<CanonicalWorkOrderRow> rows,
        CancellationToken cancellationToken,
        CanonicalSnapshotImportOptions? options = null);
}
