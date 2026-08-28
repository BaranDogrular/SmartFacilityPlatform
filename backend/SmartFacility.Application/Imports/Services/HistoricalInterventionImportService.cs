using System.Text.Json;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Domain;

namespace SmartFacility.Application.Imports.Services;

public sealed class HistoricalInterventionImportService(
    IHistoricalInterventionSourceReader sourceReader,
    IHistoricalInterventionStore store) : IHistoricalInterventionImportService
{
    public const int ExpectedCombinedSourceRows = 170_983;
    private static readonly int[] ExpectedYears = [2022, 2023, 2024, 2025, 2026];

    public async Task<HistoricalInterventionPreflightResult> PreflightAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filePaths, cancellationToken);
        var database = await store.PreflightAsync(loaded.Rows, cancellationToken);
        return CreatePreflight(loaded, database);
    }

    public async Task<ImportResult> ImportAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(filePaths, cancellationToken);
        var database = await store.PreflightAsync(loaded.Rows, cancellationToken);
        var preflight = CreatePreflight(loaded, database);
        if (!preflight.CanImport)
        {
            throw new ImportPipelineException(
                "Historical Intervention preflight failed; no database writes were made. " +
                $"Rows: {preflight.TotalRows}; parsed: {preflight.ParsedRows}; " +
                $"unmatched: {preflight.Database.UnmatchedRows}; " +
                $"ambiguous: {preflight.Database.AmbiguousRows}; " +
                $"duplicate fingerprint groups: {preflight.DuplicateFingerprintGroups}; " +
                $"conflicting identity groups: {preflight.ConflictingIdentityGroups}.");
        }

        return await store.ApplyAsync(loaded.Rows, loaded.Files, cancellationToken);
    }

    private async Task<LoadedInterventions> LoadAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        var errors = new List<string>();
        if (filePaths.Count != 5)
        {
            errors.Add($"Exactly five yearly BEAM files are required; received {filePaths.Count}.");
        }

        var fullPaths = filePaths.Select(Path.GetFullPath).ToArray();
        if (fullPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != fullPaths.Length)
        {
            errors.Add("The selected source file list contains duplicate paths.");
        }

        var files = new List<HistoricalInterventionSourceFileSummary>();
        var sourceRows = new List<HistoricalInterventionSourceRow>(ExpectedCombinedSourceRows);
        foreach (var filePath in fullPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await sourceReader.ReadAsync(filePath, cancellationToken);
            files.Add(result.File);
            sourceRows.AddRange(result.Rows);
            errors.AddRange(result.Errors);
        }

        var actualYears = sourceRows.Select(row => row.SourceYear).Distinct().Order().ToArray();
        if (!actualYears.SequenceEqual(ExpectedYears))
        {
            errors.Add(
                $"Expected declared source years 2022-2026; found {string.Join(", ", actualYears)}.");
        }

        var rows = sourceRows.Select(Prepare).ToArray();
        return new LoadedInterventions(files, rows, errors);
    }

    private static HistoricalInterventionImportRow Prepare(HistoricalInterventionSourceRow row)
    {
        var identity = CanonicalWorkOrderIdentityCalculator.Calculate(
            row.WorkOrderNumber,
            row.ReportedDateTime,
            row.AssetCode);
        var quality = HistoricalInterventionQualityClassifier.Classify(row.WorkPerformedDescription);
        var requestSanitized = HistoricalInterventionPrivacyRedactor.Redact(row.RequestDescription);
        var workPerformedSanitized = HistoricalInterventionPrivacyRedactor.Redact(
            row.WorkPerformedDescription);
        var reasonSanitized = HistoricalInterventionPrivacyRedactor.Redact(
            row.FailureReasonDescription);
        var fingerprint = HistoricalInterventionFingerprintCalculator.Calculate(row, identity);

        var auditRawData = JsonSerializer.Serialize(new
        {
            row.SourceYear,
            row.WorkOrderNumber,
            row.ReportedDateTime,
            row.AssetCode,
            row.WorkOrderStatus,
            row.AssetName,
            row.CompletionDateTime,
            RequestDescription = requestSanitized,
            WorkPerformedDescription = workPerformedSanitized,
            row.FailureReasonCode,
            FailureReasonDescription = reasonSanitized,
            row.MaintenanceDurationRaw,
            row.DowntimeDurationRaw,
            row.LaborDurationRaw,
            row.MaterialCostRaw,
            row.LaborCostRaw,
            row.TotalCostRaw,
            row.TotalCostCurrencyRaw
        });

        return new HistoricalInterventionImportRow(
            row,
            identity,
            fingerprint,
            quality,
            requestSanitized,
            workPerformedSanitized,
            reasonSanitized,
            auditRawData);
    }

    private static HistoricalInterventionPreflightResult CreatePreflight(
        LoadedInterventions loaded,
        HistoricalInterventionDatabasePreflight database)
    {
        var totalRows = loaded.Files.Sum(file => Math.Max(0, file.PhysicalRows - 1));
        var parsedRows = loaded.Rows.Count;
        var fingerprintGroups = loaded.Rows
            .GroupBy(row => row.SourceRowFingerprint, StringComparer.Ordinal)
            .ToArray();
        var duplicateFingerprintGroups = fingerprintGroups.Count(group => group.Count() > 1);
        var duplicateFingerprintRows = fingerprintGroups
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count() - 1);
        var conflictingIdentityGroups = loaded.Rows
            .GroupBy(row => row.CanonicalIdentityFingerprint, StringComparer.Ordinal)
            .Count(group => group.Select(row => row.SourceRowFingerprint).Distinct().Count() > 1);
        var years = loaded.Rows
            .GroupBy(row => row.Source.SourceYear)
            .OrderBy(group => group.Key)
            .Select(group => new HistoricalInterventionYearSummary(
                group.Key,
                group.Count(),
                group.Count(row => row.InterventionQuality == HistoricalInterventionQuality.Informative),
                group.Count(row => row.InterventionQuality == HistoricalInterventionQuality.Generic),
                group.Count(row => row.InterventionQuality == HistoricalInterventionQuality.NoAction),
                group.Count(row => string.IsNullOrWhiteSpace(row.Source.WorkPerformedDescription)),
                group.Count(row =>
                    row.InterventionQuality == HistoricalInterventionQuality.Informative
                    && HistoricalInterventionContextClassifier.IsUsable(
                        row.Source.RequestDescription))))
            .ToArray();

        return new HistoricalInterventionPreflightResult(
            loaded.Files,
            totalRows,
            parsedRows,
            Math.Max(0, totalRows - parsedRows),
            loaded.Rows.Count(row => row.InterventionQuality == HistoricalInterventionQuality.Informative),
            loaded.Rows.Count(row => row.InterventionQuality == HistoricalInterventionQuality.Generic),
            loaded.Rows.Count(row => row.InterventionQuality == HistoricalInterventionQuality.NoAction),
            loaded.Rows.Count(row => string.IsNullOrWhiteSpace(row.Source.WorkPerformedDescription)),
            fingerprintGroups.Length,
            duplicateFingerprintGroups,
            duplicateFingerprintRows,
            conflictingIdentityGroups,
            loaded.Rows.Count(row =>
                row.InterventionQuality == HistoricalInterventionQuality.Informative
                && HistoricalInterventionContextClassifier.IsUsable(row.Source.RequestDescription)),
            years,
            database,
            loaded.Errors);
    }

    private sealed record LoadedInterventions(
        IReadOnlyList<HistoricalInterventionSourceFileSummary> Files,
        IReadOnlyList<HistoricalInterventionImportRow> Rows,
        IReadOnlyList<string> Errors);
}
