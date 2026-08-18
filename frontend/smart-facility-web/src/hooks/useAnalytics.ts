import { useQuery } from '@tanstack/react-query'
import {
  getAssetOverview,
  getImportQualityOverview,
  getScadaOverview,
  getScadaTrend,
  getWorkOrderOverview,
  getWorkOrderTrend,
} from '../api/analyticsClient'
import type {
  AssetOverviewQuery,
  ScadaAnalyticsQuery,
  WorkOrderAnalyticsQuery,
} from '../api/analyticsTypes'

export const useAssetOverview = (query: AssetOverviewQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'overview', query],
    queryFn: () => getAssetOverview(query),
  })

export const useWorkOrderOverview = (query: WorkOrderAnalyticsQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'overview', query],
    queryFn: () => getWorkOrderOverview(query),
  })

export const useWorkOrderTrend = (query: WorkOrderAnalyticsQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'trend', query],
    queryFn: () => getWorkOrderTrend(query),
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

export const useImportQualityOverview = () =>
  useQuery({
    queryKey: ['analytics', 'import-quality', 'overview'],
    queryFn: getImportQualityOverview,
  })
