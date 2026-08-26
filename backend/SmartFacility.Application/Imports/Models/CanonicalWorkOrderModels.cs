namespace SmartFacility.Application.Imports.Models;

public sealed record CanonicalWorkOrderRow(
    string SourceSheet,
    int SourceRowNumber,
    string RowFingerprint,
    string IdentityFingerprint,
    string RawData,
    string? RawFormulaData,
    string WorkOrderNumber,
    DateTime ReportedDateTime,
    string AssetCode,
    string? Description,
    string? Discipline,
    string? RequestedByName,
    string? AssignedPersonnelName,
    string? Status,
    string? WorkType,
    string? FailureType,
    string? FailureReason,
    string? LocationName,
    string? ResponseDurationRaw,
    string? DowntimeRaw,
    string? MaintenanceDurationRaw,
    string? TotalCostRaw,
    string? ServiceCostRaw,
    string? RawStatusCode);

public sealed record CanonicalWorkOrderDatabasePreflight(
    long ExistingCanonicalCount,
    long MatchedExistingCount,
    long UnresolvedAssetRowCount,
    IReadOnlyList<string> UnresolvedAssetCodes,
    long AmbiguousLocationRowCount,
    IReadOnlyList<string> AmbiguousLocationNames,
    IReadOnlyList<string> ExistingIdentityCollisions);

public sealed record CanonicalWorkOrderPreflightResult(
    int TotalRows,
    int OpenRows,
    int ClosedRows,
    int OtherRows,
    int DistinctAssetCodes,
    int DuplicateIdentityCount,
    DateTime? MinReportedDateTime,
    DateTime? MaxReportedDateTime,
    CanonicalWorkOrderDatabasePreflight Database,
    IReadOnlyList<string> Errors)
{
    public bool CanImport => Errors.Count == 0
        && DuplicateIdentityCount == 0
        && Database.ExistingIdentityCollisions.Count == 0;
}
