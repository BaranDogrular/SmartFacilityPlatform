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

public sealed record Asset360ParentAssetDto(
    long AssetId,
    string AssetCode,
    string AssetName);

public sealed record Asset360IdentityDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    string? AssetType,
    string? Status,
    long? BuildingId,
    string? BuildingName,
    long? LocationId,
    string? LocationName,
    long? AssetGroupId,
    string? AssetGroupName,
    Asset360ParentAssetDto? ParentAsset,
    string? SerialNumber,
    DateTime? LastMaintenanceDate);

public sealed record Asset360MaintenanceSummaryDto(
    long TotalWorkOrders,
    long OpenWorkOrders,
    long Last7Count,
    long Last30Count,
    long Last90Count,
    DateTime? LastWorkOrderDate);

public sealed record Asset360InspectionPriorityDto(
    decimal Score,
    InspectionPriorityLevel Level,
    long Last7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long OpenCount,
    long ActivityChange,
    IReadOnlyList<string> Reasons,
    InspectionPriorityAnalysisWindowDto? AnalysisWindow,
    string ScoringVersion);

public sealed record Asset360EarlyWarningComponentsDto(
    decimal Acceleration,
    decimal ShortTermSpike,
    decimal HistoricalDeviation,
    decimal RecurrenceBurst,
    decimal OpenEmergence);

public sealed record Asset360EarlyWarningDto(
    decimal? Score,
    EarlyWarningLevel? Level,
    EarlyWarningBaselineStatus BaselineStatus,
    long Last7Count,
    long Previous7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long Previous90Count,
    decimal? BaselineMedian,
    decimal? BaselineMad,
    int BaselineActiveMonths,
    decimal? Deviation,
    long OpenCount,
    IReadOnlyList<string> Reasons,
    Asset360EarlyWarningComponentsDto? Components,
    EarlyWarningBaselineWindowDto? BaselineWindow,
    string ScoringVersion);

public sealed record Asset360ScopeDto(
    KpiReliability Reliability,
    long LinkedCanonicalWorkOrders,
    long ExcludedUnlinkedCanonicalWorkOrders,
    decimal LinkageCoveragePercent,
    bool HistoricalWorkOrdersExcluded,
    bool ScadaAndOutagesExcluded,
    string SourceDataset,
    IReadOnlyList<string> Notes);

public sealed record Asset360SummaryResponse(
    DateOnly? AsOf,
    Asset360IdentityDto Identity,
    Asset360MaintenanceSummaryDto Maintenance,
    Asset360InspectionPriorityDto InspectionPriority,
    Asset360EarlyWarningDto EarlyWarning,
    Asset360ScopeDto Scope,
    DateTimeOffset GeneratedAt);

public enum AssetActivityState
{
    [JsonStringEnumMemberName("OPEN")]
    Open,

    [JsonStringEnumMemberName("CLOSED")]
    Closed,

    [JsonStringEnumMemberName("OTHER")]
    Other
}

public enum AssetActivityInterventionQuality
{
    [JsonStringEnumMemberName("INFORMATIVE")]
    Informative,

    [JsonStringEnumMemberName("GENERIC")]
    Generic,

    [JsonStringEnumMemberName("NO_ACTION")]
    NoAction
}

public sealed record AssetActivityHistoricalInterventionDto(
    string? RequestDescription,
    string? FailureReasonDescription,
    string? WorkPerformedDescription,
    AssetActivityInterventionQuality Quality,
    DateTime? ObservedCompletionDateTime);

public sealed record AssetActivityItemDto(
    long WorkOrderId,
    string WorkOrderNumber,
    DateTime? ReportedDateTime,
    AssetActivityState State,
    string? Status,
    string? Discipline,
    string? WorkType,
    string? FailureType,
    string DescriptionSnippet,
    AssetActivityHistoricalInterventionDto? HistoricalIntervention,
    int InterventionCount);

public sealed record AssetActivityResponse(
    long AssetId,
    IReadOnlyList<AssetActivityItemDto> Items,
    int PageSize,
    bool HasNextPage,
    string? NextCursor,
    string SourceDataset,
    string PrivacyRuleVersion);

public enum AssetActivityResultStatus
{
    Success,
    AssetNotFound,
    InvalidCursor,
    StaleCursor
}

public sealed record AssetActivityResult(
    AssetActivityResultStatus Status,
    AssetActivityResponse? Response);

public sealed record AssetSearchItemDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    string? BuildingName,
    string? LocationName,
    string? AssetGroupName);

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

public enum EarlyWarningLevel
{
    [JsonStringEnumMemberName("HIGH")]
    High,

    [JsonStringEnumMemberName("MEDIUM")]
    Medium,

    [JsonStringEnumMemberName("NORMAL")]
    Normal
}

public enum EarlyWarningBaselineStatus
{
    [JsonStringEnumMemberName("SUFFICIENT")]
    Sufficient,

    [JsonStringEnumMemberName("INSUFFICIENT_BASELINE")]
    InsufficientBaseline
}

public sealed record EarlyWarningBaselineWindowDto(
    DateOnly From,
    DateOnly Through,
    int MonthCount,
    int MinimumActiveMonths);

public sealed record EarlyWarningMetadataDto(
    DateOnly? AsOf,
    EarlyWarningBaselineWindowDto? BaselineWindow,
    long TotalAssetsConsidered,
    long EligibleAssets,
    long InsufficientBaselineAssets,
    long EligibleWorkOrders,
    long ExcludedUnlinkedWorkOrders,
    decimal CoveragePercent,
    int AppliedTop,
    string SourceDataset,
    string ScoringVersion,
    IReadOnlyList<string> Notes);

public sealed record EarlyWarningItemDto(
    long AssetId,
    string AssetCode,
    string AssetName,
    decimal? WarningScore,
    EarlyWarningLevel? WarningLevel,
    EarlyWarningBaselineStatus BaselineStatus,
    long Last7Count,
    long Previous7Count,
    long Last30Count,
    long Previous30Count,
    long Last90Count,
    long Previous90Count,
    decimal? BaselineMedian,
    decimal? BaselineMad,
    int BaselineActiveMonths,
    decimal? Deviation,
    long OpenCount,
    IReadOnlyList<string> Reasons);

public sealed record EarlyWarningResponse(
    EarlyWarningMetadataDto Metadata,
    IReadOnlyList<EarlyWarningItemDto> Items);

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

public enum SimilarCasesRetrievalMode
{
    [JsonStringEnumMemberName("SAME_ASSET_DISCIPLINE")]
    SameAssetDiscipline,

    [JsonStringEnumMemberName("ASSET_GROUP_DISCIPLINE")]
    AssetGroupDiscipline,

    [JsonStringEnumMemberName("NOT_AVAILABLE")]
    NotAvailable
}

public sealed record SimilarCasesTargetAssetDto(
    long? AssetId,
    string? AssetCode,
    string? AssetName);

public sealed record SimilarCasesMetadataDto(
    long TargetWorkOrderId,
    DateTime? TargetReportedDateTime,
    SimilarCasesTargetAssetDto TargetAsset,
    string? TargetDiscipline,
    SimilarCasesRetrievalMode RetrievalMode,
    int CandidateCount,
    int ReturnedCount,
    int DuplicateTemplatesSuppressed,
    DateTime? TemporalCutoff,
    int CandidatePoolCap,
    string AlgorithmVersion,
    string? AvailabilityMessage);

public enum SimilarCaseInterventionQuality
{
    [JsonStringEnumMemberName("INFORMATIVE")]
    Informative,

    [JsonStringEnumMemberName("GENERIC")]
    Generic,

    [JsonStringEnumMemberName("NO_ACTION")]
    NoAction
}

public sealed record SimilarCaseHistoricalInterventionDto(
    string? RequestDescription,
    string? FailureReasonDescription,
    string? WorkPerformedDescription,
    SimilarCaseInterventionQuality Quality,
    DateTime? CompletionDateTime);

public sealed record SimilarCaseItemDto(
    long WorkOrderId,
    string WorkOrderNumber,
    DateTime ReportedDateTime,
    string? AssetCode,
    string? AssetName,
    string? Discipline,
    string? WorkType,
    string? FailureType,
    string? FailureReason,
    decimal SimilarityScore,
    IReadOnlyList<string> SimilarityReasons,
    string DescriptionSnippet,
    SimilarCaseHistoricalInterventionDto? HistoricalIntervention);

public sealed record SimilarCasesResponse(
    SimilarCasesMetadataDto Metadata,
    IReadOnlyList<SimilarCaseItemDto> Items);

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
