using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Abstractions;

public interface IHistoricalInterventionImportService
{
    Task<HistoricalInterventionPreflightResult> PreflightAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default);
}

public interface IHistoricalInterventionSourceReader
{
    Task<HistoricalInterventionSourceReadResult> ReadAsync(
        string filePath,
        CancellationToken cancellationToken);
}

public interface IHistoricalInterventionStore
{
    Task<HistoricalInterventionDatabasePreflight> PreflightAsync(
        IReadOnlyList<HistoricalInterventionImportRow> rows,
        CancellationToken cancellationToken);

    Task<ImportResult> ApplyAsync(
        IReadOnlyList<HistoricalInterventionImportRow> rows,
        IReadOnlyList<HistoricalInterventionSourceFileSummary> files,
        CancellationToken cancellationToken);
}
