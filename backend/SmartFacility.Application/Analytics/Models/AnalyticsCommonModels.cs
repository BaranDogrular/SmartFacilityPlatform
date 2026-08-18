namespace SmartFacility.Application.Analytics.Models;

public enum KpiReliability
{
    Green,
    Yellow,
    Red
}

public enum TimeGrain
{
    Month
}

public sealed record CategoryCountDto(string Category, long Count);

public sealed record DimensionCountDto(long? Id, string Name, long Count);

public sealed record TrendPointDto(DateOnly Period, long Count);

public sealed record QualitySummaryDto(
    long ValidRecordCount,
    long ExcludedByQualityCount);

public sealed record SnapshotAnalyticsMetadataDto(
    KpiReliability Reliability,
    string SourceDataset,
    DateTimeOffset DataAsOf,
    long SampleSize,
    IReadOnlyList<string> Notes);

public sealed record DateRangeMetadataDto(
    KpiReliability Reliability,
    string SourceDataset,
    DateTimeOffset DataAsOf,
    DateOnly? RequestedDateFrom,
    DateOnly? RequestedDateTo,
    DateTime? ActualMinDate,
    DateTime? ActualMaxDate,
    string DateField,
    long MatchedRecordCount,
    long ValidRecordCount,
    long ExcludedByQualityCount,
    string TimeZoneAssumption,
    string QualityRuleVersion,
    IReadOnlyList<string> Notes);
