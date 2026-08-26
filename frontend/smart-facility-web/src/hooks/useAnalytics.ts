import { useQuery } from '@tanstack/react-query'
import {
  getAssetMaintenanceActivityPareto,
  getAssetOverview,
  getInspectionPriority,
  getWorkOrderActivity,
  getImportQualityOverview,
  getScadaClearanceInterval,
  getScadaOverview,
  getScadaTrend,
  getWorkOrderOverview,
  getWorkOrderTrend,
} from '../api/analyticsClient'
import type {
  AssetMaintenanceActivityParetoQuery,
  AssetOverviewQuery,
  InspectionPriorityQuery,
  WorkOrderActivityQuery,
  ScadaAnalyticsQuery,
  ScadaClearanceIntervalQuery,
  WorkOrderAnalyticsQuery,
} from '../api/analyticsTypes'

export const useAssetOverview = (query: AssetOverviewQuery = {}) =>
  useQuery({
    queryKey: ['analytics', 'assets', 'overview', query],
    queryFn: () => getAssetOverview(query),
  })

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

export const useWorkOrderActivity = (
  query: WorkOrderActivityQuery = {},
) =>
  useQuery({
    queryKey: ['analytics', 'work-orders', 'activity', query],
    queryFn: () => getWorkOrderActivity(query),
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
