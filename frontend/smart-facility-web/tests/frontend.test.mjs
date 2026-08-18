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

function createQueryClient() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } })
  client.setQueryData(['analytics', 'assets', 'overview', {}], asset)
  client.setQueryData(['analytics', 'work-orders', 'overview', {}], workOrders)
  client.setQueryData(['analytics', 'work-orders', 'trend', { grain: 'Month' }], workOrderTrend)
  client.setQueryData(['analytics', 'scada', 'overview', {}], scada)
  client.setQueryData(['analytics', 'scada', 'trend', { grain: 'Month' }], scadaTrend)
  client.setQueryData(['analytics', 'import-quality', 'overview'], importQuality)
  return client
}

function renderPage(Component) {
  return renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client: createQueryClient() },
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
  const error = renderToStaticMarkup(React.createElement(dashboardUi.ErrorState, { error: new Error('Servis kullanılamıyor') }))
  const empty = renderToStaticMarkup(React.createElement(dashboardUi.EmptyState))
  const yellow = renderToStaticMarkup(React.createElement(dashboardUi.ReliabilityBadge, { reliability: 'Yellow' }))

  assert.match(loading, /role="status"/)
  assert.match(error, /role="alert"/)
  assert.match(error, /Servis kullanılamıyor/)
  assert.match(empty, /Seçilen filtrelerle eşleşen kayıt bulunamadı/)
  assert.match(yellow, /Veri kalitesi notu/)
})

test('work-order filters and reset control are present with exact raw options', () => {
  const html = renderPage(WorkOrdersPage)
  assert.match(html, /Başlangıç tarihi/)
  assert.match(html, /Bitiş tarihi/)
  assert.match(html, /value="MEKANİK"/)
  assert.match(html, /value="İŞ TESLİM EDİLDİ"/)
  assert.match(html, />Temizle</)
  assert.match(html, /yalnız güncel WorkOrder veri kaynağını içerir/)
})

test('SCADA dashboard exposes quality metadata and source filter values', () => {
  const html = renderPage(ScadaPage)
  assert.match(html, /Kalite nedeniyle dışlanan/)
  assert.match(html, />37</)
  assert.match(html, /value="ELEKTRİK ARIZALARI"/)
  assert.match(html, /value="KAMPÜS TAKİP"/)
  assert.match(html, /Veri kalitesi notu/)
})
