import assert from 'node:assert/strict'
import { after, test } from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
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
  assetsWithCurrentWorkOrders: 767,
  assetsWithoutCurrentWorkOrders: 4637,
  topAssetsByWorkOrderCount: [{ assetId: 1, assetCode: 'VRL-1', assetName: 'Pompa', workOrderCount: 42 }],
  topAssetsReliability: 'Yellow',
  metadata: { ...snapshotMetadata, sampleSize: 5404 },
}

const assetPareto = {
  totalCurrentWorkOrders: 54823,
  assetsWithCurrentWorkOrders: 767,
  appliedTop: 10,
  topAssets: [
    {
      assetId: 651,
      assetCode: '2001KBL00009',
      assetName: 'SCADA LOKASYON KONTROL ÇALIŞMASI',
      currentWorkOrderCount: 12372,
      sharePercent: 22.5672,
      cumulativeSharePercent: 22.5672,
    },
    {
      assetId: 647,
      assetCode: '2001KBL00002',
      assetName: 'ELEKTRİK LOKASYON KONTROL ÇALIŞMASI',
      currentWorkOrderCount: 4219,
      sharePercent: 7.6957,
      cumulativeSharePercent: 30.2628,
    },
  ],
  metadata: {
    ...dateMetadata,
    reliability: 'Yellow',
    sourceDataset: 'core.WorkOrders + core.Assets',
    matchedRecordCount: 54823,
    validRecordCount: 54823,
  },
}

const workOrders = {
  totalWorkOrders: 54823,
  byDiscipline: [{ category: 'MEKANİK', count: 30000 }],
  byWorkType: [{ category: 'ARIZA', count: 24000 }],
  byStatus: [{ category: 'İŞ TESLİM EDİLDİ', count: 20000 }],
  byFailureType: [{ category: 'MEKANİK ARIZA', count: 12000 }],
  byBuilding: [{ id: 1, name: 'A Blok', count: 18000 }],
  byLocation: [{ id: 1, name: 'Teknik Alan', count: 4000 }],
  byBuildingReliability: 'Yellow',
  byLocationReliability: 'Yellow',
  metadata: { ...dateMetadata, matchedRecordCount: 54823, validRecordCount: 54823 },
}

const workOrderTrend = {
  grain: 'Month',
  points: [{ period: '2026-08-01', count: 54823 }],
  metadata: { ...dateMetadata, matchedRecordCount: 54823, validRecordCount: 54823 },
}

const historicalActivity = {
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
    sourceDataset: 'analytics.HistoricalWorkOrders',
    matchedRecordCount: 167143,
    validRecordCount: 167143,
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
  client.setQueryData(['analytics', 'work-orders', 'overview', {}], workOrders)
  client.setQueryData(['analytics', 'work-orders', 'trend', { grain: 'Month' }], workOrderTrend)
  client.setQueryData(
    ['analytics', 'historical-work-orders', 'activity', {}],
    overrides.historicalActivity ?? historicalActivity,
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
    await analytics.getHistoricalMaintenanceActivity({ dateTo: '2025-12-31', discipline: 'MEKANİK' })
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
      url: '/api/analytics/historical-work-orders/activity',
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
  assert.match(html, /5\.404/)
  assert.match(html, /54\.823/)
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

  assert.match(html, /Current İş Emri Aktivitesi En Yoğun Asset&#x27;ler/)
  assert.match(html, /Top-10 asset&#x27;in toplam current iş emri kayıtlarındaki payı/)
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
      totalCurrentWorkOrders: 0,
      assetsWithCurrentWorkOrders: 0,
      topAssets: [],
      metadata: { ...assetPareto.metadata, matchedRecordCount: 0, validRecordCount: 0 },
    },
  })

  assert.match(html, /current iş emri aktivitesi bulunan asset yok/)
  assert.match(html, /YELLOW · Veri kalitesi notu/)
})

test('work-order filters and reset control are present with exact raw options', () => {
  const html = renderPage(WorkOrdersPage)
  assert.match(html, /Başlangıç tarihi/)
  assert.match(html, /Bitiş tarihi/)
  assert.match(html, /value="MEKANİK"/)
  assert.match(html, /value="İŞ TESLİM EDİLDİ"/)
  assert.match(html, />Temizle</)
  assert.match(html, /yalnız güncel WorkOrder veri kaynağını içerir/)
  assert.match(html, /Historical İş Emri Aktivitesi/)
  assert.match(html, /name="historicalDateFrom"/)
  assert.match(html, /name="historicalDateTo"/)
  assert.match(html, /name="historicalDiscipline"/)
  assert.match(html, />Uygula</)
  assert.match(html, /167\.143/)
  assert.match(html, /MEKANİK: 51\.327/)
  assert.match(html, /GREEN · Doğrulanmış metrik/)
  assert.match(html, /current WorkOrder verisi dahil değildir/)
  assert.match(html, /analytics\.HistoricalWorkOrders/)
  assert.match(html, /asset güvenilirliği değildir/)
})

test('historical activity keeps filters visible and renders an empty state without current totals', () => {
  const html = renderPage(WorkOrdersPage, {
    historicalActivity: {
      ...historicalActivity,
      trend: [],
      byDiscipline: [],
      appliedDiscipline: '__NO_MATCH__',
      metadata: { ...historicalActivity.metadata, matchedRecordCount: 0, validRecordCount: 0 },
    },
  })

  assert.match(html, /Historical İş Emri Aktivitesi/)
  assert.match(html, /Seçilen historical filtrelerle eşleşen kayıt bulunamadı/)
  assert.match(html, />Temizle</)
  assert.doesNotMatch(html, /Historical.*54\.823/s)
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
