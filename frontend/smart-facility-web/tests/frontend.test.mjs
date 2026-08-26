import assert from 'node:assert/strict'
import { after, test } from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { createServer } from 'vite'

const vite = await createServer({
  server: { middlewareMode: true },
  appType: 'custom',
  logLevel: 'silent',
})

after(async () => vite.close())

const analytics = await vite.ssrLoadModule('/src/api/analyticsClient.ts')
const dashboardUi = await vite.ssrLoadModule('/src/components/DashboardUi.tsx')
const { OverviewPage } = await vite.ssrLoadModule('/src/pages/OverviewPage.tsx')
const { AssetsPage } = await vite.ssrLoadModule('/src/pages/AssetsPage.tsx')
const { WorkOrdersPage } = await vite.ssrLoadModule('/src/pages/WorkOrdersPage.tsx')
const { InspectionPriorityPage } = await vite.ssrLoadModule('/src/pages/InspectionPriorityPage.tsx')
const { EarlyWarningPage } = await vite.ssrLoadModule('/src/pages/EarlyWarningPage.tsx')
const { AppLayout } = await vite.ssrLoadModule('/src/components/AppLayout.tsx')
const { ScadaPage } = await vite.ssrLoadModule('/src/pages/ScadaPage.tsx')

const snapshotMetadata = {
  reliability: 'Green',
  sourceDataset: 'test',
  dataAsOf: '2026-08-18T12:00:00+03:00',
  sampleSize: 1,
  notes: [],
}

const dateMetadata = {
  reliability: 'Green',
  sourceDataset: 'test',
  dataAsOf: '2026-08-18T12:00:00+03:00',
  requestedDateFrom: null,
  requestedDateTo: null,
  actualMinDate: '2026-01-01T08:00:00',
  actualMaxDate: '2026-08-01T09:00:00',
  dateField: 'ReportedDateTime',
  matchedRecordCount: 1,
  validRecordCount: 1,
  excludedByQualityCount: 0,
  timeZoneAssumption: 'UnspecifiedSourceLocal',
  qualityRuleVersion: 'test/v1',
  notes: [],
}

const asset = {
  totalAssetCount: 5404,
  countByBuilding: [{ id: 1, name: 'A Blok', count: 3200 }],
  countByLocation: [{ id: 1, name: 'Teknik Alan', count: 640 }],
  countByAssetGroup: [{ id: 1, name: 'Mekanik', count: 2200 }],
  assetsWithWorkOrders: 1488,
  assetsWithoutWorkOrders: 3916,
  topAssetsByWorkOrderCount: [{ assetId: 1, assetCode: 'VRL-1', assetName: 'Pompa', workOrderCount: 42 }],
  topAssetsReliability: 'Yellow',
  metadata: { ...snapshotMetadata, sampleSize: 5404 },
}

const assetPareto = {
  totalWorkOrders: 171136,
  assetsWithWorkOrders: 1488,
  appliedTop: 10,
  topAssets: [
    {
      assetId: 651,
      assetCode: '2001KBL00009',
      assetName: 'SCADA LOKASYON KONTROL ÇALIŞMASI',
      workOrderCount: 12372,
      sharePercent: 22.5672,
      cumulativeSharePercent: 22.5672,
    },
    {
      assetId: 647,
      assetCode: '2001KBL00002',
      assetName: 'ELEKTRİK LOKASYON KONTROL ÇALIŞMASI',
      workOrderCount: 4219,
      sharePercent: 7.6957,
      cumulativeSharePercent: 30.2628,
    },
  ],
  metadata: {
    ...dateMetadata,
    reliability: 'Yellow',
    sourceDataset: 'core.WorkOrders + core.Assets',
    matchedRecordCount: 171136,
    validRecordCount: 171136,
  },
}

const inspectionPriority = {
  metadata: {
    asOf: '2026-08-25',
    analysisWindow: {
      last7From: '2026-08-19',
      last30From: '2026-07-27',
      previous30From: '2026-06-27',
      previous30To: '2026-07-26',
      last90From: '2026-05-28',
      through: '2026-08-25',
    },
    eligibleWorkOrders: 162590,
    excludedUnlinkedWorkOrders: 8546,
    coveragePercent: 95.0063,
    totalAssetsEvaluated: 417,
    appliedTop: 10,
    sourceDataset: 'core.WorkOrders + core.Assets',
    scoringVersion: 'inspection-priority/v1',
    notes: [],
  },
  items: [
    {
      assetId: 1,
      assetCode: 'ASSET-HIGH',
      assetName: 'High activity asset',
      priorityScore: 82.5,
      priorityLevel: 'HIGH',
      last7Count: 8,
      last30Count: 20,
      previous30Count: 4,
      last90Count: 55,
      openCount: 2,
      activityChange: 16,
      reasons: ['Son 30 günde 20 iş emri', '2 açık iş emri bulunuyor'],
    },
    {
      assetId: 2,
      assetCode: 'ASSET-MEDIUM',
      assetName: 'Medium activity asset',
      priorityScore: 35,
      priorityLevel: 'MEDIUM',
      last7Count: 2,
      last30Count: 5,
      previous30Count: 3,
      last90Count: 10,
      openCount: 0,
      activityChange: 2,
      reasons: ['Önceki 30 güne göre aktivite 2 kayıt arttı'],
    },
    {
      assetId: 3,
      assetCode: 'ASSET-LOW',
      assetName: 'Low activity asset',
      priorityScore: 8.25,
      priorityLevel: 'LOW',
      last7Count: 0,
      last30Count: 1,
      previous30Count: 1,
      last90Count: 2,
      openCount: 0,
      activityChange: 0,
      reasons: ['Son 30 günde 1 iş emri'],
    },
  ],
}

const earlyWarning = {
  metadata: {
    asOf: '2026-08-25',
    baselineWindow: {
      from: '2025-07-01',
      through: '2026-06-30',
      monthCount: 12,
      minimumActiveMonths: 6,
    },
    totalAssetsConsidered: 1283,
    eligibleAssets: 248,
    insufficientBaselineAssets: 1035,
    eligibleWorkOrders: 162590,
    excludedUnlinkedWorkOrders: 8546,
    coveragePercent: 95.0063,
    appliedTop: 10,
    sourceDataset: 'core.WorkOrders + core.Assets',
    scoringVersion: 'early-warning/v1',
    notes: [],
  },
  items: [
    {
      assetId: 1,
      assetCode: 'WARNING-HIGH',
      assetName: 'Sharp increase asset',
      warningScore: 87,
      warningLevel: 'HIGH',
      baselineStatus: 'SUFFICIENT',
      last7Count: 3,
      previous7Count: 0,
      last30Count: 3,
      previous30Count: 0,
      last90Count: 5,
      previous90Count: 4,
      baselineMedian: 0.5,
      baselineMad: 0.5,
      baselineActiveMonths: 6,
      deviation: 2.5,
      openCount: 0,
      reasons: ['Önceki 30 günde kayıt yokken son 30 günde 3 yeni aktivite oluştu'],
    },
    {
      assetId: 2,
      assetCode: 'WARNING-MEDIUM',
      assetName: 'Watch asset',
      warningScore: 42,
      warningLevel: 'MEDIUM',
      baselineStatus: 'SUFFICIENT',
      last7Count: 2,
      previous7Count: 1,
      last30Count: 7,
      previous30Count: 5,
      last90Count: 21,
      previous90Count: 18,
      baselineMedian: 4,
      baselineMad: 1,
      baselineActiveMonths: 9,
      deviation: 3,
      openCount: 1,
      reasons: ['Son 7 günlük aktivite önceki 7 güne göre 1 kayıt arttı'],
    },
    {
      assetId: 3,
      assetCode: 'WARNING-NORMAL',
      assetName: 'Stable asset',
      warningScore: 8,
      warningLevel: 'NORMAL',
      baselineStatus: 'SUFFICIENT',
      last7Count: 4,
      previous7Count: 4,
      last30Count: 20,
      previous30Count: 20,
      last90Count: 60,
      previous90Count: 60,
      baselineMedian: 20,
      baselineMad: 2,
      baselineActiveMonths: 12,
      deviation: 0,
      openCount: 0,
      reasons: ['Yakın dönem aktivitesi kişisel tarihsel baseline içinde'],
    },
    {
      assetId: 4,
      assetCode: 'NO-BASELINE',
      assetName: 'New asset',
      warningScore: null,
      warningLevel: null,
      baselineStatus: 'INSUFFICIENT_BASELINE',
      last7Count: 1,
      previous7Count: 0,
      last30Count: 1,
      previous30Count: 0,
      last90Count: 1,
      previous90Count: 0,
      baselineMedian: null,
      baselineMad: null,
      baselineActiveMonths: 1,
      deviation: null,
      openCount: 0,
      reasons: ['12 aylık baseline içinde en az 6 aktif ay gerekir; 1 aktif ay bulundu'],
    },
  ],
}

const workOrders = {
  totalWorkOrders: 171136,
  openWorkOrders: 75,
  closedWorkOrders: 171054,
  otherWorkOrders: 7,
  last30DaysWorkOrders: 1234,
  byDiscipline: [{ category: 'MEKANİK', count: 30000 }],
  byWorkType: [{ category: 'ARIZA', count: 24000 }],
  byRawStatusCode: [{ category: 'K', count: 171054 }, { category: 'A', count: 75 }, { category: 'I', count: 7 }],
  byStatus: [{ category: 'İŞ TESLİM EDİLDİ', count: 20000 }],
  byFailureType: [{ category: 'MEKANİK ARIZA', count: 12000 }],
  byBuilding: [{ id: 1, name: 'A Blok', count: 18000 }],
  byLocation: [{ id: 1, name: 'Teknik Alan', count: 4000 }],
  byBuildingReliability: 'Yellow',
  byLocationReliability: 'Yellow',
  metadata: { ...dateMetadata, matchedRecordCount: 171136, validRecordCount: 171136 },
}

const workOrderTrend = {
  grain: 'Month',
  points: [{ period: '2026-08-01', count: 54823 }],
  metadata: { ...dateMetadata, matchedRecordCount: 54823, validRecordCount: 54823 },
}

const workOrderActivity = {
  grain: 'Month',
  trend: [
    { period: '2025-01-01', count: 6144 },
    { period: '2025-02-01', count: 5488 },
  ],
  byDiscipline: [
    { category: 'ELEKTRİK / ELEKTRONİK', count: 55932 },
    { category: 'MEKANİK', count: 51327 },
  ],
  appliedDiscipline: null,
  metadata: {
    ...dateMetadata,
    reliability: 'Green',
    sourceDataset: 'core.WorkOrders',
    matchedRecordCount: 171136,
    validRecordCount: 171136,
  },
}

const scada = {
  totalAlarmOccurrences: 1950,
  bySourceSheet: [
    { category: 'YANGIN', count: 844 },
    { category: 'MEKANİK', count: 576 },
    { category: 'ENERJİ', count: 374 },
    { category: 'KAMPÜS TAKİP', count: 127 },
    { category: 'ELEKTRİK ARIZALARI', count: 29 },
  ],
  byAlarmType: [{ category: 'Uyarı', count: 1000 }],
  byInterventionLevel: [{ category: 'Seviye 1', count: 800 }],
  bySection: [{ category: 'Mekanik', count: 576 }],
  byLocationRaw: [{ category: 'Teknik Alan', count: 300 }],
  invalidOrMissingTimestampCount: 36,
  dateQualityIssueCount: 37,
  bySectionReliability: 'Yellow',
  byLocationRawReliability: 'Yellow',
  metadata: { ...dateMetadata, dateField: 'ReceivedAt', matchedRecordCount: 1950, validRecordCount: 1913, excludedByQualityCount: 37 },
}

const scadaTrend = {
  grain: 'Month',
  points: [{ period: '2026-08-01', count: 1913 }],
  quality: { validRecordCount: 1913, excludedByQualityCount: 37 },
  metadata: { ...dateMetadata, reliability: 'Yellow', dateField: 'ReceivedAt', matchedRecordCount: 1950, validRecordCount: 1913, excludedByQualityCount: 37 },
}

const scadaClearance = {
  totalMatchedOccurrences: 1950,
  eligibleOccurrences: 1750,
  excludedOccurrences: 200,
  eligibilityPercent: 89.74,
  medianMinutes: 10,
  p90Minutes: 139.1,
  appliedFilters: {
    sourceSheet: null,
    alarmType: null,
    interventionLevel: null,
    section: null,
    locationRaw: null,
  },
  metadata: {
    ...dateMetadata,
    reliability: 'Yellow',
    sourceDataset: 'core.ScadaAlarmEvents',
    dateField: 'ReceivedAt',
    matchedRecordCount: 1950,
    validRecordCount: 1750,
    excludedByQualityCount: 200,
  },
}

const importQuality = {
  totalBatches: 27,
  batchesByStatus: [
    { category: 'Completed', count: 25 },
    { category: 'Failed', count: 1 },
    { category: 'InProgress', count: 1 },
  ],
  batchesBySourceType: [{ sourceType: 'ScadaAlarm', count: 12 }],
  sourceRecordsByParseStatus: [{ category: 'Succeeded', count: 229346 }],
  importErrorCount: 2,
  errorsBySourceType: [{ sourceType: 'HistoricalWorkOrder', count: 2 }],
  fingerprintAlgorithmDistribution: [{ category: 'historical-work-order/v1', count: 222055 }],
  legacySourceRecordCount: 61075,
  versionedSourceRecordCount: 223538,
  metadata: { ...snapshotMetadata, sampleSize: 284613 },
}

function createQueryClient(overrides = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } })
  client.setQueryData(['analytics', 'assets', 'overview', {}], asset)
  client.setQueryData(
    ['analytics', 'assets', 'maintenance-activity-pareto', { top: 10 }],
    overrides.assetPareto ?? assetPareto,
  )
  client.setQueryData(
    ['analytics', 'assets', 'inspection-priority', { top: 10 }],
    overrides.inspectionPriority ?? inspectionPriority,
  )
  client.setQueryData(
    ['analytics', 'assets', 'early-warning', { top: 10 }],
    overrides.earlyWarning ?? earlyWarning,
  )
  client.setQueryData(['analytics', 'work-orders', 'overview', {}], workOrders)
  client.setQueryData(['analytics', 'work-orders', 'trend', { grain: 'Month' }], workOrderTrend)
  client.setQueryData(
    ['analytics', 'work-orders', 'activity', {}],
    overrides.workOrderActivity ?? workOrderActivity,
  )
  client.setQueryData(['analytics', 'scada', 'overview', {}], scada)
  client.setQueryData(['analytics', 'scada', 'trend', { grain: 'Month' }], scadaTrend)
  client.setQueryData(
    ['analytics', 'scada', 'clearance-interval', {}],
    overrides.scadaClearance ?? scadaClearance,
  )
  client.setQueryData(['analytics', 'import-quality', 'overview'], importQuality)
  return client
}

function renderPage(Component, overrides) {
  return renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: createQueryClient(overrides) },
      React.createElement(Component),
    ),
  )
}

test('API query values are serialized exactly and empty values are omitted', () => {
  assert.equal(
    analytics.serializeAnalyticsParams({
      dateFrom: '2026-08-01',
      discipline: 'MEKANİK',
      status: undefined,
      locationId: 4,
    }),
    'dateFrom=2026-08-01&discipline=MEKAN%C4%B0K&locationId=4',
  )

  assert.equal(
    analytics.serializeAnalyticsParams({
      dateFrom: '2025-01-01',
      dateTo: '2025-12-31',
      top: 5,
      discipline: '',
      sourceSheet: undefined,
    }),
    'dateFrom=2025-01-01&dateTo=2025-12-31&top=5',
  )
})

test('new analytics clients call the accepted backend routes with serialized query objects', async () => {
  const originalAdapter = analytics.analyticsHttpClient.defaults.adapter
  const requests = []

  analytics.analyticsHttpClient.defaults.adapter = async (config) => {
    requests.push({ url: config.url, params: config.params })
    return { data: {}, status: 200, statusText: 'OK', headers: {}, config }
  }

  try {
    await analytics.getAssetMaintenanceActivityPareto({ dateFrom: '2025-01-01', top: 5 })
    await analytics.getInspectionPriority({ asOf: '2026-08-25', top: 25 })
    await analytics.getEarlyWarning({ asOf: '2026-08-25', top: 10 })
    await analytics.getWorkOrderActivity({ dateTo: '2025-12-31', discipline: 'MEKANİK' })
    await analytics.getScadaClearanceInterval({ sourceSheet: 'MEKANİK', alarmType: 'SOĞUTMA' })
  } finally {
    analytics.analyticsHttpClient.defaults.adapter = originalAdapter
  }

  assert.deepEqual(requests, [
    {
      url: '/api/analytics/assets/maintenance-activity-pareto',
      params: { dateFrom: '2025-01-01', top: 5 },
    },
    {
      url: '/api/analytics/assets/inspection-priority',
      params: { asOf: '2026-08-25', top: 25 },
    },
    {
      url: '/api/analytics/assets/early-warning',
      params: { asOf: '2026-08-25', top: 10 },
    },
    {
      url: '/api/analytics/work-orders/activity',
      params: { dateTo: '2025-12-31', discipline: 'MEKANİK' },
    },
    {
      url: '/api/analytics/scada/clearance-interval',
      params: { sourceSheet: 'MEKANİK', alarmType: 'SOĞUTMA' },
    },
  ])
})

test('API client returns typed response data and converts validation errors', async () => {
  const originalAdapter = analytics.analyticsHttpClient.defaults.adapter
  let requestConfig

  analytics.analyticsHttpClient.defaults.adapter = async (config) => {
    requestConfig = config
    return { data: asset, status: 200, statusText: 'OK', headers: {}, config }
  }

  const response = await analytics.getAssetOverview({ top: 10 })
  assert.equal(response.totalAssetCount, 5404)
  assert.equal(requestConfig.url, '/api/analytics/assets/overview')
  assert.deepEqual(requestConfig.params, { top: 10 })

  analytics.analyticsHttpClient.defaults.adapter = async (config) => {
    const error = new Error('Bad Request')
    Object.assign(error, {
      isAxiosError: true,
      config,
      response: {
        status: 400,
        data: { errors: { dateFrom: ['Başlangıç tarihi geçersiz.'] } },
      },
    })
    throw error
  }

  await assert.rejects(
    () => analytics.getWorkOrderOverview({ dateFrom: 'invalid' }),
    (error) => error.message === 'Başlangıç tarihi geçersiz.' && error.status === 400,
  )
  analytics.analyticsHttpClient.defaults.adapter = originalAdapter
})

test('general dashboard renders production-contract KPI values without unsupported terminology', () => {
  const html = renderPage(OverviewPage)
  assert.match(html, /Bakım ve Güvenilirlik Operasyon Özeti/)
  assert.match(html, /Operasyon Özeti/)
  assert.match(html, /Bakım Aktivitesi Yoğunluğu/)
  assert.match(html, /İş Emri Disiplinleri/)
  assert.match(html, /İkincil Operasyon Durumu/)
  assert.match(html, /5\.404/)
  assert.match(html, /171\.136/)
  assert.match(html, /1\.950/)
  assert.match(html, /Import Denetim Kaydı/)
  assert.doesNotMatch(html, /MTTR|MTBF|Asset Health|Open Alarm|Total Combined WorkOrders/i)
})

test('dashboard state and reliability components render accessible text', () => {
  const loading = renderToStaticMarkup(React.createElement(dashboardUi.LoadingState))
  const error = renderToStaticMarkup(React.createElement(dashboardUi.ErrorState, {
    error: new Error('Servis kullanılamıyor'),
    onRetry: () => {},
  }))
  const empty = renderToStaticMarkup(React.createElement(dashboardUi.EmptyState))
  const yellow = renderToStaticMarkup(React.createElement(dashboardUi.ReliabilityBadge, { reliability: 'Yellow' }))

  assert.match(loading, /role="status"/)
  assert.match(error, /role="alert"/)
  assert.match(error, /Servis kullanılamıyor/)
  assert.match(error, /Yeniden dene/)
  assert.match(empty, /Seçilen filtrelerle eşleşen kayıt bulunamadı/)
  assert.match(yellow, /Veri kalitesi notu/)
})

test('asset Pareto renders Top-N counts, shares, cumulative values and Yellow guidance', () => {
  const html = renderPage(AssetsPage)

  assert.match(html, /İş Emri Aktivitesi En Yoğun Asset&#x27;ler/)
  assert.match(html, /Top-10 asset&#x27;in toplam canonical iş emri kayıtlarındaki payı/)
  assert.match(html, /2001KBL00009/)
  assert.match(html, /12\.372/)
  assert.match(html, /22,57%/)
  assert.match(html, /30,26%/)
  assert.match(html, /YELLOW · Veri kalitesi notu/)
  assert.match(html, /sağlık durumu veya arızaya yatkınlığı hakkında bir sonuç değildir/)
  assert.doesNotMatch(html, /2001KBL00009 · SCADA LOKASYON/)
  assert.doesNotMatch(html, /en kötü|en arızalı|unhealthy|failure rate|health score/i)
})

test('asset Pareto renders a scoped empty state', () => {
  const html = renderPage(AssetsPage, {
    assetPareto: {
      ...assetPareto,
      totalWorkOrders: 0,
      assetsWithWorkOrders: 0,
      topAssets: [],
      metadata: { ...assetPareto.metadata, matchedRecordCount: 0, validRecordCount: 0 },
    },
  })

  assert.match(html, /iş emri aktivitesi bulunan asset yok/)
  assert.match(html, /YELLOW · Veri kalitesi notu/)
})

test('inspection priority renders ranked signals, reasons, coverage and safe semantics', () => {
  const html = renderPage(InspectionPriorityPage)

  assert.match(html, /İnceleme Önceliği/)
  assert.match(html, /ASSET-HIGH/)
  assert.match(html, /ASSET-MEDIUM/)
  assert.match(html, /ASSET-LOW/)
  assert.match(html, /HIGH · Öncelikli inceleme/)
  assert.match(html, /MEDIUM · Yakın izleme/)
  assert.match(html, /LOW · Düşük öncelik/)
  assert.match(html, /82,5/)
  assert.match(html, /Son 30 günde 20 iş emri/)
  assert.match(html, /2 açık iş emri bulunuyor/)
  assert.match(html, /162\.590/)
  assert.match(html, /8\.546/)
  assert.match(html, /95,01%/)
  assert.match(html, /arıza olasılığı veya varlık sağlık skoru değildir/)
  assert.match(html, /inspection-table-wrap/)
  assert.doesNotMatch(html, /failure probability|predictive failure|arıza riski %/i)
})

test('inspection priority renders scoped empty, loading, error and retry states', () => {
  const emptyHtml = renderPage(InspectionPriorityPage, {
    inspectionPriority: {
      ...inspectionPriority,
      metadata: { ...inspectionPriority.metadata, totalAssetsEvaluated: 0 },
      items: [],
    },
  })
  assert.match(emptyHtml, /son 90 gün aktivitesi veya açık iş yükü bulunan bağlı varlık yok/)

  const pendingClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const pendingHtml = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: pendingClient },
      React.createElement(InspectionPriorityPage),
    ),
  )
  assert.match(pendingHtml, /İnceleme önceliği hesaplanıyor/)

  const errorClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, retryOnMount: false, refetchOnMount: false, staleTime: Infinity },
    },
  })
  const errorQuery = errorClient.getQueryCache().build(errorClient, {
    queryKey: ['analytics', 'assets', 'inspection-priority', { top: 10 }],
    queryFn: async () => inspectionPriority,
  })
  errorQuery.setState({ ...errorQuery.state, status: 'error', fetchStatus: 'idle', error: new Error('Priority servisi kullanılamıyor') })
  const errorHtml = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: errorClient },
      React.createElement(InspectionPriorityPage),
    ),
  )
  assert.match(errorHtml, /Priority servisi kullanılamıyor/)
  assert.match(errorHtml, /Yeniden dene/)
})

test('early warning renders levels, reasons, baseline coverage and safe semantics', () => {
  const html = renderPage(EarlyWarningPage)

  assert.match(html, /Erken Uyarı/)
  assert.match(html, /WARNING-HIGH/)
  assert.match(html, /WARNING-MEDIUM/)
  assert.match(html, /WARNING-NORMAL/)
  assert.match(html, /NO-BASELINE/)
  assert.match(html, /YÜKSEK SAPMA/)
  assert.match(html, /İZLE/)
  assert.match(html, /NORMAL/)
  assert.match(html, /BASELINE YETERSİZ/)
  assert.match(html, /87/)
  assert.match(html, /yeni aktivite oluştu/)
  assert.match(html, /1\.283/)
  assert.match(html, /248/)
  assert.match(html, /1\.035/)
  assert.match(html, /95,01%/)
  assert.match(html, /arıza olasılığı veya varlık sağlık skoru değildir/)
  assert.match(html, /hangi asset kendi normal davranışından sapıyor/)
  assert.match(html, /early-warning-table-wrap/)
  assert.doesNotMatch(html, /failure probability|predictive maintenance|arıza riski %/i)
})

test('early warning renders empty, loading, error and retry states', () => {
  const emptyHtml = renderPage(EarlyWarningPage, {
    earlyWarning: {
      ...earlyWarning,
      metadata: { ...earlyWarning.metadata, totalAssetsConsidered: 0, eligibleAssets: 0 },
      items: [],
    },
  })
  assert.match(emptyHtml, /değerlendirilebilecek bağlı varlık bulunamadı/)

  const pendingClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const pendingHtml = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: pendingClient },
      React.createElement(EarlyWarningPage),
    ),
  )
  assert.match(pendingHtml, /Erken uyarı aktivite sapmaları hesaplanıyor/)

  const errorClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, retryOnMount: false, refetchOnMount: false, staleTime: Infinity },
    },
  })
  const errorQuery = errorClient.getQueryCache().build(errorClient, {
    queryKey: ['analytics', 'assets', 'early-warning', { top: 10 }],
    queryFn: async () => earlyWarning,
  })
  errorQuery.setState({ ...errorQuery.state, status: 'error', fetchStatus: 'idle', error: new Error('Erken uyarı servisi kullanılamıyor') })
  const errorHtml = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: errorClient },
      React.createElement(EarlyWarningPage),
    ),
  )
  assert.match(errorHtml, /Erken uyarı servisi kullanılamıyor/)
  assert.match(errorHtml, /Yeniden dene/)
})

test('early warning navigation is exposed independently from inspection priority', () => {
  const html = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/early-warning'] },
      React.createElement(AppLayout),
    ),
  )

  assert.match(html, /href="\/early-warning"/)
  assert.match(html, /Erken Uyarı/)
  assert.match(html, /href="\/inspection-priority"/)
  assert.match(html, /İnceleme Önceliği/)
})

test('work-order filters and reset control are present with exact raw options', () => {
  const html = renderPage(WorkOrdersPage)
  assert.match(html, /Başlangıç tarihi/)
  assert.match(html, /Bitiş tarihi/)
  assert.match(html, /value="MEKANİK"/)
  assert.match(html, /value="İŞ TESLİM EDİLDİ"/)
  assert.match(html, />Temizle</)
  assert.match(html, /Açık İş Emri/)
  assert.match(html, /Kapalı İş Emri/)
  assert.match(html, /Son 30 Gün Aktivitesi/)
  assert.match(html, /İş Emri Aktivitesi/)
  assert.match(html, /name="activityDateFrom"/)
  assert.match(html, /name="activityDateTo"/)
  assert.match(html, /name="activityDiscipline"/)
  assert.match(html, />Uygula</)
  assert.match(html, /171\.136/)
  assert.match(html, /MEKANİK: 51\.327/)
  assert.match(html, /GREEN · Doğrulanmış metrik/)
  assert.match(html, /core\.WorkOrders/)
  assert.doesNotMatch(html, /Güncel İş Emri|Geçmiş İş Emri Aktivitesi/)
})

test('canonical activity keeps filters visible and renders an empty state', () => {
  const html = renderPage(WorkOrdersPage, {
    workOrderActivity: {
      ...workOrderActivity,
      trend: [],
      byDiscipline: [],
      appliedDiscipline: '__NO_MATCH__',
      metadata: { ...workOrderActivity.metadata, matchedRecordCount: 0, validRecordCount: 0 },
    },
  })

  assert.match(html, /İş Emri Aktivitesi/)
  assert.match(html, /Seçilen filtrelerle eşleşen iş emri kaydı bulunamadı/)
  assert.match(html, />Temizle</)
  assert.doesNotMatch(html, /Historical WorkOrders|current WorkOrder/i)
})

test('SCADA dashboard exposes quality metadata and source filter values', () => {
  const html = renderPage(ScadaPage)
  assert.match(html, /Kalite nedeniyle dışlanan/)
  assert.match(html, />37</)
  assert.match(html, /value="ELEKTRİK ARIZALARI"/)
  assert.match(html, /value="KAMPÜS TAKİP"/)
  assert.match(html, /Veri kalitesi notu/)
  assert.match(html, /SCADA Clearance Interval/)
  assert.match(html, /Median Clearance/)
  assert.match(html, />10 dk</)
  assert.match(html, /P90 Clearance/)
  assert.match(html, />139,1 dk</)
  assert.match(html, /Eligible occurrence <strong>1\.750</)
  assert.match(html, /Kalite nedeniyle dışlanan <strong>200</)
  assert.match(html, /89,74%/)
  assert.match(html, /YELLOW · Veri kalitesi notu/)
  assert.match(html, /source occurrence alt kümesinde hesaplanır/)
  assert.doesNotMatch(html, /MTTR|repair|tamir/i)
})

test('SCADA clearance renders null-percentile no-match state', () => {
  const html = renderPage(ScadaPage, {
    scadaClearance: {
      ...scadaClearance,
      totalMatchedOccurrences: 0,
      eligibleOccurrences: 0,
      excludedOccurrences: 0,
      eligibilityPercent: null,
      medianMinutes: null,
      p90Minutes: null,
      metadata: { ...scadaClearance.metadata, matchedRecordCount: 0, validRecordCount: 0, excludedByQualityCount: 0 },
    },
  })

  assert.match(html, /clearance aralığı hesaplanabilecek occurrence bulunamadı/)
  assert.match(html, /YELLOW · Veri kalitesi notu/)
  assert.doesNotMatch(html, /Median Clearance<\/span>[\s\S]*10 dk/)
})
