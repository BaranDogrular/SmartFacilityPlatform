import axios from 'axios'
import type {
  AssetMaintenanceActivityParetoQuery,
  AssetMaintenanceActivityParetoResponse,
  AssetOverviewQuery,
  AssetOverviewResponse,
  WorkOrderActivityQuery,
  WorkOrderActivityResponse,
  ImportQualityOverviewResponse,
  ScadaAnalyticsQuery,
  ScadaClearanceIntervalQuery,
  ScadaClearanceIntervalResponse,
  ScadaOverviewResponse,
  ScadaTrendResponse,
  WorkOrderAnalyticsQuery,
  WorkOrderOverviewResponse,
  WorkOrderTrendResponse,
} from './analyticsTypes'

interface ApiProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

type QueryValue = string | number | undefined
type QueryParameters = Record<string, QueryValue>

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim().replace(/\/$/, '')

export const analyticsHttpClient = axios.create({
  baseURL: configuredBaseUrl || '',
  timeout: 30_000,
  headers: {
    Accept: 'application/json',
  },
})

export class AnalyticsApiError extends Error {
  readonly status?: number

  constructor(message: string, status?: number) {
    super(message)
    this.name = 'AnalyticsApiError'
    this.status = status
  }
}

export function serializeAnalyticsParams(parameters: QueryParameters): string {
  const search = new URLSearchParams()

  Object.entries(parameters).forEach(([key, value]) => {
    if (value !== undefined && value !== '') {
      search.set(key, String(value))
    }
  })

  return search.toString()
}

export function toAnalyticsApiError(error: unknown): AnalyticsApiError {
  if (!axios.isAxiosError<ApiProblemDetails>(error)) {
    return new AnalyticsApiError('Veriler alınırken beklenmeyen bir sorun oluştu.')
  }

  const problem = error.response?.data
  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().filter(Boolean).join(' ')
    : undefined
  const message =
    validationMessage ||
    problem?.detail ||
    problem?.title ||
    (error.code === 'ECONNABORTED'
      ? 'İstek zaman aşımına uğradı. Lütfen tekrar deneyin.'
      : 'Analytics servisine ulaşılamadı. Lütfen bağlantıyı kontrol edin.')

  return new AnalyticsApiError(message, error.response?.status)
}

analyticsHttpClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => Promise.reject(toAnalyticsApiError(error)),
)

async function get<T>(path: string, query: QueryParameters = {}): Promise<T> {
  const response = await analyticsHttpClient.get<T>(path, {
    params: query,
    paramsSerializer: {
      serialize: (parameters) => serializeAnalyticsParams(parameters as QueryParameters),
    },
  })

  return response.data
}

export const getAssetOverview = (query: AssetOverviewQuery = {}) =>
  get<AssetOverviewResponse>('/api/analytics/assets/overview', query as QueryParameters)

export const getAssetMaintenanceActivityPareto = (
  query: AssetMaintenanceActivityParetoQuery = {},
) =>
  get<AssetMaintenanceActivityParetoResponse>(
    '/api/analytics/assets/maintenance-activity-pareto',
    query as QueryParameters,
  )

export const getWorkOrderOverview = (query: WorkOrderAnalyticsQuery = {}) =>
  get<WorkOrderOverviewResponse>('/api/analytics/work-orders/overview', query as QueryParameters)

export const getWorkOrderTrend = (query: WorkOrderAnalyticsQuery = {}) =>
  get<WorkOrderTrendResponse>('/api/analytics/work-orders/trend', query as QueryParameters)

export const getWorkOrderActivity = (
  query: WorkOrderActivityQuery = {},
) =>
  get<WorkOrderActivityResponse>(
    '/api/analytics/work-orders/activity',
    query as QueryParameters,
  )

export const getScadaOverview = (query: ScadaAnalyticsQuery = {}) =>
  get<ScadaOverviewResponse>('/api/analytics/scada/overview', query as QueryParameters)

export const getScadaTrend = (query: ScadaAnalyticsQuery = {}) =>
  get<ScadaTrendResponse>('/api/analytics/scada/trend', query as QueryParameters)

export const getScadaClearanceInterval = (query: ScadaClearanceIntervalQuery = {}) =>
  get<ScadaClearanceIntervalResponse>(
    '/api/analytics/scada/clearance-interval',
    query as QueryParameters,
  )

export const getImportQualityOverview = () =>
  get<ImportQualityOverviewResponse>('/api/analytics/import-quality/overview')
