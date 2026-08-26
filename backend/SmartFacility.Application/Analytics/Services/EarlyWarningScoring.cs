using SmartFacility.Application.Analytics.Models;

namespace SmartFacility.Application.Analytics.Services;

public static class EarlyWarningScoring
{
    public const string Version = "early-warning/v1";

    public const int BaselineMonthCount = 12;
    public const int MinimumActiveMonths = 6;

    public const decimal AccelerationWeight = 25m;
    public const decimal ShortTermSpikeWeight = 25m;
    public const decimal HistoricalDeviationWeight = 30m;
    public const decimal RecurrenceBurstWeight = 15m;
    public const decimal OpenEmergenceWeight = 5m;

    public const decimal HighThreshold = 60m;
    public const decimal MediumThreshold = 30m;
    private const decimal MadConsistencyScale = 1.4826m;

    public static EarlyWarningScoreResult Calculate(EarlyWarningSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var accelerationRatio = PositiveDifference(signals.Last30Count, signals.Previous30Count)
            / Max(signals.Previous30Count, signals.BaselineMedian, 1m);
        var shortTermSpikeRatio = PositiveDifference(signals.Last7Count, signals.Previous7Count)
            / Max(signals.Previous7Count, signals.BaselineMedian / 4m, 1m);
        var robustScale = Math.Max(1m, MadConsistencyScale * signals.BaselineMad);
        var robustDeviation = Math.Max(0m, signals.Last30Count - signals.BaselineMedian)
            / robustScale;
        var expectedRecentShare = Max(signals.Last90Count / 3m, signals.BaselineMedian, 1m);
        var recurrenceRatio = signals.Last30Count < 2
            ? 0m
            : Math.Max(0m, signals.Last30Count - expectedRecentShare) / expectedRecentShare;
        var openEmergence = signals.OpenCount > 0 && signals.BaselineOpenCount == 0;

        var components = new EarlyWarningComponents(
            Bounded(accelerationRatio, AccelerationWeight),
            Bounded(shortTermSpikeRatio, ShortTermSpikeWeight),
            Bounded(robustDeviation / 3m, HistoricalDeviationWeight),
            Bounded(recurrenceRatio, RecurrenceBurstWeight),
            openEmergence ? OpenEmergenceWeight : 0m);
        var score = Math.Round(
            Math.Clamp(components.Total, 0m, 100m),
            2,
            MidpointRounding.AwayFromZero);

        return new EarlyWarningScoreResult(
            score,
            GetLevel(score),
            Math.Round(signals.Last30Count - signals.BaselineMedian, 2, MidpointRounding.AwayFromZero),
            components,
            CreateReasons(signals, openEmergence));
    }

    public static EarlyWarningLevel GetLevel(decimal score) =>
        score >= HighThreshold
            ? EarlyWarningLevel.High
            : score >= MediumThreshold
                ? EarlyWarningLevel.Medium
                : EarlyWarningLevel.Normal;

    public static decimal Median(IReadOnlyCollection<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return 0m;
        }

        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;
    }

    public static decimal MedianAbsoluteDeviation(
        IReadOnlyCollection<long> values,
        decimal median) =>
        Median(values
            .Select(value => (long)Math.Round(
                Math.Abs(value - median) * 2m,
                0,
                MidpointRounding.AwayFromZero))
            .ToArray()) / 2m;

    private static decimal Bounded(decimal ratio, decimal weight) =>
        weight * Math.Clamp(ratio, 0m, 1m);

    private static decimal PositiveDifference(long current, long previous) =>
        Math.Max(0m, current - previous);

    private static decimal Max(decimal first, decimal second, decimal third) =>
        Math.Max(first, Math.Max(second, third));

    private static IReadOnlyList<string> CreateReasons(
        EarlyWarningSignals signals,
        bool openEmergence)
    {
        var reasons = new List<string>(5);

        if (signals.Last30Count > signals.Previous30Count)
        {
            reasons.Add(signals.Previous30Count == 0
                ? $"Önceki 30 günde kayıt yokken son 30 günde {signals.Last30Count} yeni aktivite oluştu"
                : $"Son 30 günlük aktivite önceki döneme göre {signals.Last30Count - signals.Previous30Count} kayıt arttı");
        }

        if (signals.Last7Count > signals.Previous7Count)
        {
            reasons.Add($"Son 7 günlük aktivite önceki 7 güne göre {signals.Last7Count - signals.Previous7Count} kayıt arttı");
        }

        if (signals.Last30Count > signals.BaselineMedian)
        {
            reasons.Add($"Son 30 günlük aktivite {signals.BaselineMedian:0.##} tarihsel median seviyesinin üzerinde");
        }

        var expectedRecentShare = Max(signals.Last90Count / 3m, signals.BaselineMedian, 1m);
        if (signals.Last30Count >= 2 && signals.Last30Count > expectedRecentShare)
        {
            reasons.Add("Son 90 günlük aktivite yakın dönemde belirgin biçimde kümelendi");
        }

        if (openEmergence)
        {
            reasons.Add($"Tarihsel baseline'da açık kayıt yokken {signals.OpenCount} açık iş emri bulunuyor");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Yakın dönem aktivitesi kişisel tarihsel baseline içinde");
        }

        return reasons;
    }
}

public sealed record EarlyWarningSignals(
    long Last7Count,
    long Previous7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long OpenCount,
    long BaselineOpenCount,
    decimal BaselineMedian,
    decimal BaselineMad);

public sealed record EarlyWarningComponents(
    decimal Acceleration,
    decimal ShortTermSpike,
    decimal HistoricalDeviation,
    decimal RecurrenceBurst,
    decimal OpenEmergence)
{
    public decimal Total =>
        Acceleration + ShortTermSpike + HistoricalDeviation + RecurrenceBurst + OpenEmergence;
}

public sealed record EarlyWarningScoreResult(
    decimal Score,
    EarlyWarningLevel Level,
    decimal Deviation,
    EarlyWarningComponents Components,
    IReadOnlyList<string> Reasons);
