using System.Globalization;
using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Services;

public static class CanonicalSnapshotCompletenessGuard
{
    public const decimal MinimumFullSnapshotRatio = 0.90m;

    public const string InitialSnapshotStatus = "InitialSnapshot";
    public const string CompleteStatus = "Complete";
    public const string BlockedStatus = "BlockedSuspiciousShrink";
    public const string OverrideStatus = "AllowedByExplicitOverride";

    public static CanonicalSnapshotCompletenessDecision Evaluate(
        long currentActiveCount,
        long sourceRowCount,
        long expectedFinalActiveCount,
        long expectedInactiveCount,
        bool allowSuspiciousSnapshotShrink)
    {
        var sourceShrinkPercent = Percentage(
            Math.Max(0, currentActiveCount - expectedFinalActiveCount),
            currentActiveCount);
        var expectedInactivationPercent = Percentage(
            expectedInactiveCount,
            currentActiveCount);
        var suspiciousShrink = currentActiveCount > 0
            && expectedFinalActiveCount < currentActiveCount * MinimumFullSnapshotRatio;
        var overrideApplied = suspiciousShrink && allowSuspiciousSnapshotShrink;

        if (!suspiciousShrink)
        {
            return new CanonicalSnapshotCompletenessDecision(
                currentActiveCount == 0 ? InitialSnapshotStatus : CompleteStatus,
                true,
                false,
                sourceShrinkPercent,
                expectedInactivationPercent,
                []);
        }

        var action = overrideApplied
            ? "allowed only because the explicit suspicious-shrink override was requested"
            : "rejected";
        var warning = string.Create(
            CultureInfo.InvariantCulture,
            $"Canonical snapshot {action} because the source contains {sourceRowCount} rows " +
            $"while {currentActiveCount} canonical records are currently active. Applying this " +
            $"snapshot would inactivate {expectedInactiveCount} records " +
            $"({expectedInactivationPercent:F2}%) and reduce active membership by " +
            $"{sourceShrinkPercent:F2}%.");

        return new CanonicalSnapshotCompletenessDecision(
            overrideApplied ? OverrideStatus : BlockedStatus,
            overrideApplied,
            overrideApplied,
            sourceShrinkPercent,
            expectedInactivationPercent,
            [warning]);
    }

    private static decimal Percentage(long numerator, long denominator) =>
        denominator == 0
            ? 0m
            : decimal.Round(numerator * 100m / denominator, 4, MidpointRounding.AwayFromZero);
}

public sealed record CanonicalSnapshotCompletenessDecision(
    string Status,
    bool IsAllowed,
    bool OverrideApplied,
    decimal SourceShrinkPercent,
    decimal ExpectedInactivationPercent,
    IReadOnlyList<string> SafetyWarnings);
