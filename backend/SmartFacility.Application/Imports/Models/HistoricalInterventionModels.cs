using SmartFacility.Domain;

namespace SmartFacility.Application.Imports.Models;

public sealed record HistoricalInterventionSourceRow(
    string SourceFilePath,
    string SourceFileName,
    string FileSha256,
    string SourceSheet,
    int SourceRowNumber,
    int SourceYear,
    string WorkOrderNumber,
    DateTime ReportedDateTime,
    string AssetCode,
    string? WorkOrderStatus,
    string? AssetName,
    DateTime? CompletionDateTime,
    string? RequestDescription,
    string? WorkPerformedDescription,
    string? FailureReasonCode,
    string? FailureReasonDescription,
    string? MaintenanceDurationRaw,
    string? DowntimeDurationRaw,
    string? LaborDurationRaw,
    string? MaterialCostRaw,
    string? LaborCostRaw,
    string? TotalCostRaw,
    string? TotalCostCurrencyRaw);

public sealed record HistoricalInterventionSourceFileSummary(
    string FilePath,
    string FileName,
    string FileSha256,
    long FileSizeBytes,
    string SheetName,
    int PhysicalRows,
    int ParsedRows,
    DateTime? MinReportedDateTime,
    DateTime? MaxReportedDateTime);

public sealed record HistoricalInterventionSourceReadResult(
    HistoricalInterventionSourceFileSummary File,
    IReadOnlyList<HistoricalInterventionSourceRow> Rows,
    IReadOnlyList<string> Errors);

public sealed record HistoricalInterventionImportRow(
    HistoricalInterventionSourceRow Source,
    string CanonicalIdentityFingerprint,
    string SourceRowFingerprint,
    HistoricalInterventionQuality InterventionQuality,
    string? RequestDescriptionSanitized,
    string? WorkPerformedDescriptionSanitized,
    string? FailureReasonDescriptionSanitized,
    string AuditRawData);

public sealed record HistoricalInterventionYearSummary(
    int SourceYear,
    int TotalRows,
    int InformativeRows,
    int GenericRows,
    int NoActionRows,
    int BlankActionTextRows,
    int ProblemAndInformativeRows);

public sealed record HistoricalInterventionDatabasePreflight(
    bool HistoricalInterventionSchemaExists,
    long StrictCanonicalMatches,
    long ActiveCanonicalMatches,
    long InactiveCanonicalMatches,
    long UnmatchedRows,
    long AmbiguousRows,
    long ExistingRows,
    long ExpectedInserts,
    long ExpectedUnchanged,
    IReadOnlyList<string> UnmatchedReferences,
    IReadOnlyList<string> AmbiguousReferences);

public sealed record HistoricalInterventionPreflightResult(
    IReadOnlyList<HistoricalInterventionSourceFileSummary> Files,
    int TotalRows,
    int ParsedRows,
    int InvalidRows,
    int InformativeInterventionCount,
    int GenericInterventionCount,
    int NoActionInterventionCount,
    int BlankActionTextCount,
    int DistinctFingerprints,
    int DuplicateFingerprintGroups,
    int DuplicateFingerprintRows,
    int ConflictingIdentityGroups,
    int ProblemAndInformativeCount,
    IReadOnlyList<HistoricalInterventionYearSummary> Years,
    HistoricalInterventionDatabasePreflight Database,
    IReadOnlyList<string> Errors)
{
    public bool CanImport =>
        Files.Count == 5
        && TotalRows == 170_983
        && ParsedRows == TotalRows
        && InvalidRows == 0
        && Database.StrictCanonicalMatches == TotalRows
        && Database.UnmatchedRows == 0
        && Database.AmbiguousRows == 0
        && DuplicateFingerprintGroups == 0
        && ConflictingIdentityGroups == 0
        && Errors.Count == 0;
}
