using System.Text.Json.Serialization;

namespace SmartFacility.Application.Analytics.Models;

public sealed record AssetWorkOrderCountDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    long WorkOrderCount);

public sealed record AssetOverviewResponse(
    long TotalAssetCount,
    IReadOnlyList<DimensionCountDto> CountByBuilding,
    IReadOnlyList<DimensionCountDto> CountByLocation,
    IReadOnlyList<DimensionCountDto> CountByAssetGroup,
    long AssetsWithWorkOrders,
    long AssetsWithoutWorkOrders,
    IReadOnlyList<AssetWorkOrderCountDto> TopAssetsByWorkOrderCount,
    KpiReliability TopAssetsReliability,
    SnapshotAnalyticsMetadataDto Metadata);

public sealed record AssetMaintenanceActivityParetoItemDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    long WorkOrderCount,
    decimal SharePercent,
    decimal CumulativeSharePercent);

public sealed record AssetMaintenanceActivityParetoResponse(
    long TotalWorkOrders,
    long AssetsWithWorkOrders,
    int AppliedTop,
    IReadOnlyList<AssetMaintenanceActivityParetoItemDto> TopAssets,
    DateRangeMetadataDto Metadata);

public enum InspectionPriorityLevel
{
    [JsonStringEnumMemberName("HIGH")]
    High,

    [JsonStringEnumMemberName("MEDIUM")]
    Medium,

    [JsonStringEnumMemberName("LOW")]
    Low
}

public sealed record InspectionPriorityAnalysisWindowDto(
    DateOnly Last7From,
    DateOnly Last30From,
    DateOnly Previous30From,
    DateOnly Previous30To,
    DateOnly Last90From,
    DateOnly Through);

public sealed record InspectionPriorityMetadataDto(
    DateOnly? AsOf,
    InspectionPriorityAnalysisWindowDto? AnalysisWindow,
    long EligibleWorkOrders,
    long ExcludedUnlinkedWorkOrders,
    decimal CoveragePercent,
    long TotalAssetsEvaluated,
    int AppliedTop,
    string SourceDataset,
    string ScoringVersion,
    IReadOnlyList<string> Notes);

public sealed record InspectionPriorityItemDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    decimal PriorityScore,
    InspectionPriorityLevel PriorityLevel,
    long Last7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long OpenCount,
    long ActivityChange,
    IReadOnlyList<string> Reasons);

public sealed record InspectionPriorityResponse(
    InspectionPriorityMetadataDto Metadata,
    IReadOnlyList<InspectionPriorityItemDto> Items);

public sealed record WorkOrderOverviewResponse(
    long TotalWorkOrders,
    long OpenWorkOrders,
    long ClosedWorkOrders,
    long OtherWorkOrders,
    long Last30DaysWorkOrders,
    IReadOnlyList<CategoryCountDto> ByDiscipline,
    IReadOnlyList<CategoryCountDto> ByWorkType,
    IReadOnlyList<CategoryCountDto> ByRawStatusCode,
    IReadOnlyList<CategoryCountDto> ByStatus,
    IReadOnlyList<CategoryCountDto> ByFailureType,
    IReadOnlyList<DimensionCountDto> ByBuilding,
    IReadOnlyList<DimensionCountDto> ByLocation,
    KpiReliability ByBuildingReliability,
    KpiReliability ByLocationReliability,
    DateRangeMetadataDto Metadata);

public sealed record WorkOrderTrendResponse(
    TimeGrain Grain,
    IReadOnlyList<TrendPointDto> Points,
    DateRangeMetadataDto Metadata);

public sealed record WorkOrderActivityResponse(
    TimeGrain Grain,
    IReadOnlyList<TrendPointDto> Trend,
    IReadOnlyList<CategoryCountDto> ByDiscipline,
    string? AppliedDiscipline,
    DateRangeMetadataDto Metadata);

public sealed record ScadaOverviewResponse(
    long TotalAlarmOccurrences,
    IReadOnlyList<CategoryCountDto> BySourceSheet,
    IReadOnlyList<CategoryCountDto> ByAlarmType,
    IReadOnlyList<CategoryCountDto> ByInterventionLevel,
    IReadOnlyList<CategoryCountDto> BySection,
    IReadOnlyList<CategoryCountDto> ByLocationRaw,
    long InvalidOrMissingTimestampCount,
    long DateQualityIssueCount,
    KpiReliability BySectionReliability,
    KpiReliability ByLocationRawReliability,
    DateRangeMetadataDto Metadata);

public sealed record ScadaTrendResponse(
    TimeGrain Grain,
    IReadOnlyList<TrendPointDto> Points,
    QualitySummaryDto Quality,
    DateRangeMetadataDto Metadata);

public sealed record ScadaClearanceIntervalAppliedFiltersDto(
    string? SourceSheet,
    string? AlarmType,
    string? InterventionLevel,
    string? Section,
    string? LocationRaw);

public sealed record ScadaClearanceIntervalResponse(
    long TotalMatchedOccurrences,
    long EligibleOccurrences,
    long ExcludedOccurrences,
    decimal? EligibilityPercent,
    decimal? MedianMinutes,
    decimal? P90Minutes,
    ScadaClearanceIntervalAppliedFiltersDto AppliedFilters,
    DateRangeMetadataDto Metadata);

public sealed record SourceTypeBatchCountDto(
    string SourceType,
    long Count);

public sealed record ImportQualityOverviewResponse(
    long TotalBatches,
    IReadOnlyList<CategoryCountDto> BatchesByStatus,
    IReadOnlyList<SourceTypeBatchCountDto> BatchesBySourceType,
    IReadOnlyList<CategoryCountDto> SourceRecordsByParseStatus,
    long ImportErrorCount,
    IReadOnlyList<SourceTypeBatchCountDto> ErrorsBySourceType,
    IReadOnlyList<CategoryCountDto> FingerprintAlgorithmDistribution,
    long LegacySourceRecordCount,
    long VersionedSourceRecordCount,
    SnapshotAnalyticsMetadataDto Metadata);
