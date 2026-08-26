using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Application.Analytics.Services;

public static class InspectionPriorityScoring
{
    public const string Version = "inspection-priority/v1";

    public const decimal RecentActivityWeight = 30m;
    public const long RecentActivityCap = 21;
    public const decimal ActivityAccelerationWeight = 20m;
    public const long ActivityAccelerationCap = 8;
    public const decimal OpenWorkloadWeight = 25m;
    public const long OpenWorkloadCap = 4;
    public const decimal RecurrenceWeight = 15m;
    public const long RecurrenceCap = 50;
    public const decimal VeryRecentActivityWeight = 10m;
    public const long VeryRecentActivityCap = 7;

    public const decimal HighThreshold = 50m;
    public const decimal MediumThreshold = 25m;

    public static InspectionPriorityScoreResult Calculate(InspectionPrioritySignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var activityChange = signals.Last30Count - signals.Previous30Count;
        var score =
            ScoreBounded(signals.Last30Count, RecentActivityCap, RecentActivityWeight)
            + ScoreBounded(Math.Max(activityChange, 0), ActivityAccelerationCap, ActivityAccelerationWeight)
            + ScoreBounded(signals.OpenCount, OpenWorkloadCap, OpenWorkloadWeight)
            + ScoreBounded(signals.Last90Count, RecurrenceCap, RecurrenceWeight)
            + ScoreBounded(signals.Last7Count, VeryRecentActivityCap, VeryRecentActivityWeight);
        var boundedScore = Math.Round(
            Math.Clamp(score, 0m, 100m),
            2,
            MidpointRounding.AwayFromZero);

        return new InspectionPriorityScoreResult(
            boundedScore,
            GetLevel(boundedScore),
            activityChange,
            CreateReasons(signals, activityChange));
    }

    public static InspectionPriorityLevel GetLevel(decimal score) =>
        score >= HighThreshold
            ? InspectionPriorityLevel.High
            : score >= MediumThreshold
                ? InspectionPriorityLevel.Medium
                : InspectionPriorityLevel.Low;

    private static decimal ScoreBounded(long value, long cap, decimal weight) =>
        value <= 0
            ? 0m
            : weight * Math.Min(value / (decimal)cap, 1m);

    private static IReadOnlyList<string> CreateReasons(
        InspectionPrioritySignals signals,
        long activityChange)
    {
        var reasons = new List<string>(5);

        if (signals.Last30Count > 0)
        {
            reasons.Add($"Son 30 günde {signals.Last30Count} iş emri");
        }

        if (activityChange > 0)
        {
            reasons.Add(signals.Previous30Count == 0
                ? "Önceki 30 günde kayıt yokken yakın dönem aktivitesi başladı"
                : $"Önceki 30 güne göre aktivite {activityChange} kayıt arttı");
        }

        if (signals.OpenCount > 0)
        {
            reasons.Add($"{signals.OpenCount} açık iş emri bulunuyor");
        }

        if (signals.Last7Count >= 2)
        {
            reasons.Add($"Son 7 günde {signals.Last7Count} iş emri ile tekrarlayan aktivite görüldü");
        }

        if (signals.Last90Count >= RecurrenceCap)
        {
            reasons.Add($"Son 90 günde {signals.Last90Count} iş emri kaydı bulunuyor");
        }
        else if (reasons.Count == 0 && signals.Last90Count > 0)
        {
            reasons.Add($"Son 90 günde {signals.Last90Count} iş emri");
        }

        return reasons;
    }
}

public sealed record InspectionPrioritySignals(
    long Last7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long OpenCount);

public sealed record InspectionPriorityScoreResult(
    decimal Score,
    InspectionPriorityLevel Level,
    long ActivityChange,
    IReadOnlyList<string> Reasons);
