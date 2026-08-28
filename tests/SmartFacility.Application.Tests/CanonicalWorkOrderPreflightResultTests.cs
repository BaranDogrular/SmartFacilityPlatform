using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;

namespace SmartFacility.Application.Tests;

public sealed class CanonicalWorkOrderPreflightResultTests
{
    [Fact]
    public void Explicit_shrink_override_does_not_bypass_structural_errors()
    {
        var result = Result(
            Database(allowOverride: true, overrideApplied: true),
            errors: ["İş Emirleri: expected header was not found."]);

        Assert.False(result.CanImport);
        Assert.True(result.AllowSuspiciousSnapshotShrink);
        Assert.True(result.SuspiciousSnapshotShrinkOverrideApplied);
    }

    [Fact]
    public void Explicit_shrink_override_does_not_bypass_incoming_identity_collisions()
    {
        var result = Result(
            Database(allowOverride: true, overrideApplied: true),
            duplicateIdentityCount: 1);

        Assert.False(result.CanImport);
    }

    [Fact]
    public void Explicit_shrink_override_does_not_bypass_existing_identity_collisions()
    {
        var result = Result(Database(
            allowOverride: true,
            overrideApplied: true,
            existingIdentityCollisions: ["collision"]));

        Assert.False(result.CanImport);
    }

    [Fact]
    public void Explicit_override_allows_only_the_completeness_decision()
    {
        var result = Result(Database(allowOverride: true, overrideApplied: true));

        Assert.True(result.CanImport);
        Assert.Equal(CanonicalSnapshotCompletenessGuard.OverrideStatus, result.SnapshotCompletenessStatus);
        Assert.Single(result.SafetyWarnings);
    }

    private static CanonicalWorkOrderPreflightResult Result(
        CanonicalWorkOrderDatabasePreflight database,
        int duplicateIdentityCount = 0,
        IReadOnlyList<string>? errors = null) =>
        new(
            TotalRows: 50,
            OpenRows: 50,
            ClosedRows: 0,
            OtherRows: 0,
            DistinctAssetCodes: 10,
            DuplicateIdentityCount: duplicateIdentityCount,
            MinReportedDateTime: new DateTime(2026, 1, 1),
            MaxReportedDateTime: new DateTime(2026, 1, 2),
            Database: database,
            Errors: errors ?? []);

    private static CanonicalWorkOrderDatabasePreflight Database(
        bool allowOverride,
        bool overrideApplied,
        IReadOnlyList<string>? existingIdentityCollisions = null) =>
        new(
            CurrentActiveCount: 200,
            SourceRowCount: 50,
            MatchedExistingCount: 50,
            ExpectedUnchangedCount: 50,
            ExpectedInsertCount: 0,
            ExpectedUpdateCount: 0,
            ExpectedInactiveCount: 150,
            ExpectedReactivationCount: 0,
            ExpectedFinalActiveCount: 50,
            SourceShrinkPercent: 75m,
            ExpectedInactivationPercent: 75m,
            SnapshotCompletenessStatus: overrideApplied
                ? CanonicalSnapshotCompletenessGuard.OverrideStatus
                : CanonicalSnapshotCompletenessGuard.BlockedStatus,
            AllowSuspiciousSnapshotShrink: allowOverride,
            SuspiciousSnapshotShrinkOverrideApplied: overrideApplied,
            IsSnapshotCompletenessAllowed: overrideApplied,
            SafetyWarnings: ["suspicious shrink"],
            UnresolvedAssetRowCount: 0,
            UnresolvedAssetCodes: [],
            AmbiguousLocationRowCount: 0,
            AmbiguousLocationNames: [],
            ExistingIdentityCollisions: existingIdentityCollisions ?? []);
}
