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
    long AssetsWithCurrentWorkOrders,
    long AssetsWithoutCurrentWorkOrders,
    IReadOnlyList<AssetWorkOrderCountDto> TopAssetsByWorkOrderCount,
    KpiReliability TopAssetsReliability,
    SnapshotAnalyticsMetadataDto Metadata);

public sealed record WorkOrderOverviewResponse(
    long TotalWorkOrders,
    IReadOnlyList<CategoryCountDto> ByDiscipline,
    IReadOnlyList<CategoryCountDto> ByWorkType,
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
