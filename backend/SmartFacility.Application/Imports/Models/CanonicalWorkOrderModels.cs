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

public sealed record CanonicalSnapshotImportOptions(
    bool AllowSuspiciousSnapshotShrink = false);

public sealed record CanonicalWorkOrderDatabasePreflight(
    long CurrentActiveCount,
    long SourceRowCount,
    long MatchedExistingCount,
    long ExpectedUnchangedCount,
    long ExpectedInsertCount,
    long ExpectedUpdateCount,
    long ExpectedInactiveCount,
    long ExpectedReactivationCount,
    long ExpectedFinalActiveCount,
    decimal SourceShrinkPercent,
    decimal ExpectedInactivationPercent,
    string SnapshotCompletenessStatus,
    bool AllowSuspiciousSnapshotShrink,
    bool SuspiciousSnapshotShrinkOverrideApplied,
    bool IsSnapshotCompletenessAllowed,
    IReadOnlyList<string> SafetyWarnings,
    long UnresolvedAssetRowCount,
    IReadOnlyList<string> UnresolvedAssetCodes,
    long AmbiguousLocationRowCount,
    IReadOnlyList<string> AmbiguousLocationNames,
    IReadOnlyList<string> ExistingIdentityCollisions)
{
    public long ExistingCanonicalCount => CurrentActiveCount;
}

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
    public int SourceRowCount => TotalRows;
    public long CurrentActiveCount => Database.CurrentActiveCount;
    public long MatchedExistingCount => Database.MatchedExistingCount;
    public long ExpectedUnchangedCount => Database.ExpectedUnchangedCount;
    public long ExpectedInsertCount => Database.ExpectedInsertCount;
    public long ExpectedUpdateCount => Database.ExpectedUpdateCount;
    public long ExpectedInactiveCount => Database.ExpectedInactiveCount;
    public long ExpectedReactivationCount => Database.ExpectedReactivationCount;
    public long ExpectedFinalActiveCount => Database.ExpectedFinalActiveCount;
    public decimal SourceShrinkPercent => Database.SourceShrinkPercent;
    public decimal ExpectedInactivationPercent => Database.ExpectedInactivationPercent;
    public string SnapshotCompletenessStatus => Database.SnapshotCompletenessStatus;
    public bool AllowSuspiciousSnapshotShrink => Database.AllowSuspiciousSnapshotShrink;
    public bool SuspiciousSnapshotShrinkOverrideApplied =>
        Database.SuspiciousSnapshotShrinkOverrideApplied;
    public IReadOnlyList<string> SafetyWarnings => Database.SafetyWarnings;

    public bool CanImport => Errors.Count == 0
        && DuplicateIdentityCount == 0
        && Database.ExistingIdentityCollisions.Count == 0
        && Database.IsSnapshotCompletenessAllowed;
}
