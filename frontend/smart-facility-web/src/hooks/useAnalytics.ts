import { useQuery } from '@tanstack/react-query'
import {
  getAssetActivity,
  getAssetMaintenanceActivityPareto,
  getAssetOverview,
  getAsset360Summary,
  getInspectionPriority,
  getEarlyWarning,
  getWorkOrderActivity,
  getSimilarCases,
  getImportQualityOverview,
  getScadaClearanceInterval,
  getScadaOverview,
  getScadaTrend,
  getWorkOrderOverview,
  getWorkOrderTrend,
  searchAssets,
} from '../api/analyticsClient'
import type {
  AssetMaintenanceActivityParetoQuery,
  AssetOverviewQuery,
  InspectionPriorityQuery,
  EarlyWarningQuery,
  WorkOrderActivityQuery,
  SimilarCasesQuery,
  ScadaAnalyticsQuery,
  ScadaClearanceIntervalQuery,
  WorkOrderAnalyticsQuery,
} from '../api/analyticsTypes'

export const useAssetOverview = (query: AssetOverviewQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'overview', query],
    queryFn: () => getAssetOverview(query),
  })

export const useAsset360Summary = (assetId: number, enabled = true) =>
  useQuery({
    queryKey: ['analytics', 'assets', assetId, 'summary'],
    queryFn: () => getAsset360Summary(assetId),
    enabled,
  })

export const assetActivityQueryKey = (
  assetId: number,
  cursor: string | null,
  pageSize: number,
) => ['analytics', 'assets', assetId, 'activity', { cursor, pageSize }] as const

export const useAssetActivity = (
  assetId: number,
  cursor: string | null,
  pageSize = 25,
  enabled = true,
) =>
  useQuery({
    queryKey: assetActivityQueryKey(assetId, cursor, pageSize),
    queryFn: ({ signal }) => getAssetActivity(
      assetId,
      { pageSize, ...(cursor ? { cursor } : {}) },
      signal,
    ),
    enabled: enabled && Number.isSafeInteger(assetId) && assetId > 0,
    retry: false,
  })

export const assetSearchQueryKey = (query: string, limit: number) =>
  ['analytics', 'assets', 'search', { q: query.trim(), limit }] as const

export const useAssetSearch = (
  query: string,
  requestedLimit = 10,
  enabled = true,
) => {
  const normalizedQuery = query.trim()
  const limit = Math.min(Math.max(requestedLimit, 1), 10)

  return useQuery({
    queryKey: assetSearchQueryKey(normalizedQuery, limit),
    queryFn: ({ signal }) => searchAssets({ q: normalizedQuery, limit }, signal),
    enabled: enabled && normalizedQuery.length >= 2 && normalizedQuery.length <= 100,
    retry: false,
  })
}

export const useAssetMaintenanceActivityPareto = (
  query: AssetMaintenanceActivityParetoQuery = {},
) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'maintenance-activity-pareto', query],
    queryFn: () => getAssetMaintenanceActivityPareto(query),
  })

export const useInspectionPriority = (query: InspectionPriorityQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'inspection-priority', query],
    queryFn: () => getInspectionPriority(query),
  })

export const useEarlyWarning = (query: EarlyWarningQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'early-warning', query],
    queryFn: () => getEarlyWarning(query),
  })

export const useWorkOrderOverview = (query: WorkOrderAnalyticsQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'overview', query],
    queryFn: () => getWorkOrderOverview(query),
  })

export const useWorkOrderTrend = (query: WorkOrderAnalyticsQuery = {}, enabled = true) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'trend', query],
    queryFn: () => getWorkOrderTrend(query),
    enabled,
  })

export const useWorkOrderActivity = (
  query: WorkOrderActivityQuery = {},
) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'activity', query],
    queryFn: () => getWorkOrderActivity(query),
  })

export const useSimilarCases = (
  workOrderId: number,
  query: SimilarCasesQuery = {},
  enabled = true,
) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', workOrderId, 'similar-cases', query],
    queryFn: () => getSimilarCases(workOrderId, query),
    enabled,
  })

export const useScadaOverview = (query: ScadaAnalyticsQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'scada', 'overview', query],
    queryFn: () => getScadaOverview(query),
  })

export const useScadaTrend = (query: ScadaAnalyticsQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'scada', 'trend', query],
    queryFn: () => getScadaTrend(query),
  })

export const useScadaClearanceInterval = (query: ScadaClearanceIntervalQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'scada', 'clearance-interval', query],
    queryFn: () => getScadaClearanceInterval(query),
  })

export const useImportQualityOverview = () =>
  useQuery({
    queryKey: ['analytics', 'import-quality', 'overview'],
    queryFn: getImportQualityOverview,
  })
