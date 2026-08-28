export type KpiReliability = 'Green' | 'Yellow' | 'Red'

export type TimeGrain = 'Month'

export interface CategoryCount {
  category: string
  count: number
}

export interface DimensionCount {
  id: number | null
  name: string
  count: number
}

export interface TrendPoint {
  period: string
  count: number
}

export interface QualitySummary {
  validRecordCount: number
  excludedByQualityCount: number
}

export interface SnapshotAnalyticsMetadata {
  reliability: KpiReliability
  sourceDataset: string
  dataAsOf: string
  sampleSize: number
  notes: string[]
}

export interface DateRangeMetadata {
  reliability: KpiReliability
  sourceDataset: string
  dataAsOf: string
  requestedDateFrom: string | null
  requestedDateTo: string | null
  actualMinDate: string | null
  actualMaxDate: string | null
  dateField: string
  matchedRecordCount: number
  validRecordCount: number
  excludedByQualityCount: number
  timeZoneAssumption: string
  qualityRuleVersion: string
  notes: string[]
}

export interface AssetWorkOrderCount {
  assetId: number
  assetCode: string
  assetName: string
  workOrderCount: number
}

export interface AssetOverviewResponse {
  totalAssetCount: number
  countByBuilding: DimensionCount[]
  countByLocation: DimensionCount[]
  countByAssetGroup: DimensionCount[]
  assetsWithWorkOrders: number
  assetsWithoutWorkOrders: number
  topAssetsByWorkOrderCount: AssetWorkOrderCount[]
  topAssetsReliability: KpiReliability
  metadata: SnapshotAnalyticsMetadata
}

export interface AssetMaintenanceActivityParetoItem {
  assetId: number
  assetCode: string
  assetName: string
  workOrderCount: number
  sharePercent: number
  cumulativeSharePercent: number
}

export interface AssetMaintenanceActivityParetoResponse {
  totalWorkOrders: number
  assetsWithWorkOrders: number
  appliedTop: number
  topAssets: AssetMaintenanceActivityParetoItem[]
  metadata: DateRangeMetadata
}

export type InspectionPriorityLevel = 'HIGH' | 'MEDIUM' | 'LOW'

export interface InspectionPriorityAnalysisWindow {
  last7From: string
  last30From: string
  previous30From: string
  previous30To: string
  last90From: string
  through: string
}

export interface InspectionPriorityMetadata {
  asOf: string | null
  analysisWindow: InspectionPriorityAnalysisWindow | null
  eligibleWorkOrders: number
  excludedUnlinkedWorkOrders: number
  coveragePercent: number
  totalAssetsEvaluated: number
  appliedTop: number
  sourceDataset: string
  scoringVersion: string
  notes: string[]
}

export interface InspectionPriorityItem {
  assetId: number
  assetCode: string
  assetName: string
  priorityScore: number
  priorityLevel: InspectionPriorityLevel
  last7Count: number
  last30Count: number
  previous30Count: number
  last90Count: number
  openCount: number
  activityChange: number
  reasons: string[]
}

export interface InspectionPriorityResponse {
  metadata: InspectionPriorityMetadata
  items: InspectionPriorityItem[]
}

export type EarlyWarningLevel = 'HIGH' | 'MEDIUM' | 'NORMAL'
export type EarlyWarningBaselineStatus = 'SUFFICIENT' | 'INSUFFICIENT_BASELINE'

export interface EarlyWarningBaselineWindow {
  from: string
  through: string
  monthCount: number
  minimumActiveMonths: number
}

export interface EarlyWarningMetadata {
  asOf: string | null
  baselineWindow: EarlyWarningBaselineWindow | null
  totalAssetsConsidered: number
  eligibleAssets: number
  insufficientBaselineAssets: number
  eligibleWorkOrders: number
  excludedUnlinkedWorkOrders: number
  coveragePercent: number
  appliedTop: number
  sourceDataset: string
  scoringVersion: string
  notes: string[]
}

export interface EarlyWarningItem {
  assetId: number
  assetCode: string
  assetName: string
  warningScore: number | null
  warningLevel: EarlyWarningLevel | null
  baselineStatus: EarlyWarningBaselineStatus
  last7Count: number
  previous7Count: number
  last30Count: number
  previous30Count: number
  last90Count: number
  previous90Count: number
  baselineMedian: number | null
  baselineMad: number | null
  baselineActiveMonths: number
  deviation: number | null
  openCount: number
  reasons: string[]
}

export interface EarlyWarningResponse {
  metadata: EarlyWarningMetadata
  items: EarlyWarningItem[]
}

export interface WorkOrderOverviewResponse {
  totalWorkOrders: number
  openWorkOrders: number
  closedWorkOrders: number
  otherWorkOrders: number
  last30DaysWorkOrders: number
  byDiscipline: CategoryCount[]
  byWorkType: CategoryCount[]
  byRawStatusCode: CategoryCount[]
  byStatus: CategoryCount[]
  byFailureType: CategoryCount[]
  byBuilding: DimensionCount[]
  byLocation: DimensionCount[]
  byBuildingReliability: KpiReliability
  byLocationReliability: KpiReliability
  metadata: DateRangeMetadata
}

export interface WorkOrderTrendResponse {
  grain: TimeGrain
  points: TrendPoint[]
  metadata: DateRangeMetadata
}

export type SimilarCasesRetrievalMode =
  | 'SAME_ASSET_DISCIPLINE'
  | 'ASSET_GROUP_DISCIPLINE'
  | 'NOT_AVAILABLE'

export interface SimilarCasesTargetAsset {
  assetId: number | null
  assetCode: string | null
  assetName: string | null
}

export interface SimilarCasesMetadata {
  targetWorkOrderId: number
  targetReportedDateTime: string | null
  targetAsset: SimilarCasesTargetAsset
  targetDiscipline: string | null
  retrievalMode: SimilarCasesRetrievalMode
  candidateCount: number
  returnedCount: number
  duplicateTemplatesSuppressed: number
  temporalCutoff: string | null
  candidatePoolCap: number
  algorithmVersion: string
  availabilityMessage: string | null
}

export interface SimilarCaseItem {
  workOrderId: number
  workOrderNumber: string
  reportedDateTime: string
  assetCode: string | null
  assetName: string | null
  discipline: string | null
  workType: string | null
  failureType: string | null
  failureReason: string | null
  similarityScore: number
  similarityReasons: string[]
  descriptionSnippet: string
  historicalIntervention: SimilarCaseHistoricalIntervention | null
}

export type SimilarCaseInterventionQuality = 'INFORMATIVE' | 'GENERIC' | 'NO_ACTION'

export interface SimilarCaseHistoricalIntervention {
  requestDescription: string | null
  failureReasonDescription: string | null
  workPerformedDescription: string | null
  quality: SimilarCaseInterventionQuality
  completionDateTime: string | null
}

export interface SimilarCasesResponse {
  metadata: SimilarCasesMetadata
  items: SimilarCaseItem[]
}

export interface WorkOrderActivityResponse {
  grain: TimeGrain
  trend: TrendPoint[]
  byDiscipline: CategoryCount[]
  appliedDiscipline: string | null
  metadata: DateRangeMetadata
}

export interface ScadaOverviewResponse {
  totalAlarmOccurrences: number
  bySourceSheet: CategoryCount[]
  byAlarmType: CategoryCount[]
  byInterventionLevel: CategoryCount[]
  bySection: CategoryCount[]
  byLocationRaw: CategoryCount[]
  invalidOrMissingTimestampCount: number
  dateQualityIssueCount: number
  bySectionReliability: KpiReliability
  byLocationRawReliability: KpiReliability
  metadata: DateRangeMetadata
}

export interface ScadaTrendResponse {
  grain: TimeGrain
  points: TrendPoint[]
  quality: QualitySummary
  metadata: DateRangeMetadata
}

export interface ScadaClearanceIntervalAppliedFilters {
  sourceSheet: string | null
  alarmType: string | null
  interventionLevel: string | null
  section: string | null
  locationRaw: string | null
}

export interface ScadaClearanceIntervalResponse {
  totalMatchedOccurrences: number
  eligibleOccurrences: number
  excludedOccurrences: number
  eligibilityPercent: number | null
  medianMinutes: number | null
  p90Minutes: number | null
  appliedFilters: ScadaClearanceIntervalAppliedFilters
  metadata: DateRangeMetadata
}

export interface SourceTypeBatchCount {
  sourceType: string
  count: number
}

export interface ImportQualityOverviewResponse {
  totalBatches: number
  batchesByStatus: CategoryCount[]
  batchesBySourceType: SourceTypeBatchCount[]
  sourceRecordsByParseStatus: CategoryCount[]
  importErrorCount: number
  errorsBySourceType: SourceTypeBatchCount[]
  fingerprintAlgorithmDistribution: CategoryCount[]
  legacySourceRecordCount: number
  versionedSourceRecordCount: number
  metadata: SnapshotAnalyticsMetadata
}

export interface AssetOverviewQuery {
  buildingId?: number
  locationId?: number
  assetGroupId?: number
  assetId?: number
  workOrderDateFrom?: string
  workOrderDateTo?: string
  top?: number
}

export interface AssetMaintenanceActivityParetoQuery {
  dateFrom?: string
  dateTo?: string
  top?: number
}

export interface InspectionPriorityQuery {
  top?: number
  asOf?: string
}

export interface EarlyWarningQuery {
  top?: number
  asOf?: string
}

export interface WorkOrderAnalyticsQuery {
  dateFrom?: string
  dateTo?: string
  discipline?: string
  workType?: string
  status?: string
  failureType?: string
  buildingId?: number
  locationId?: number
  assetId?: number
  grain?: TimeGrain
}

export interface SimilarCasesQuery {
  top?: number
}

export interface ScadaAnalyticsQuery {
  dateFrom?: string
  dateTo?: string
  sourceSheet?: string
  alarmType?: string
  interventionLevel?: string
  section?: string
  locationRaw?: string
  grain?: TimeGrain
}

export interface WorkOrderActivityQuery {
  dateFrom?: string
  dateTo?: string
  discipline?: string
}

export type ScadaClearanceIntervalQuery = Omit<ScadaAnalyticsQuery, 'grain'>
