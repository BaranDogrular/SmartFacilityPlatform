import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { after, test } from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
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
const { Asset360Page } = await vite.ssrLoadModule('/src/pages/Asset360Page.tsx')
const { AssetSearch } = await vite.ssrLoadModule('/src/components/AssetSearch.tsx')
const { WorkOrdersPage } = await vite.ssrLoadModule('/src/pages/WorkOrdersPage.tsx')
const { SimilarCasesPage } = await vite.ssrLoadModule('/src/pages/SimilarCasesPage.tsx')
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

const asset360 = {
  asOf: '2026-08-25',
  identity: {
    assetId: 360,
    assetCode: 'A-360',
    assetName: 'Ana Soğutma Pompası',
    assetType: 'Pompa',
    status: 'Kullanımda',
    buildingId: 1,
    buildingName: 'A Blok',
    locationId: 2,
    locationName: 'Mekanik Oda',
    assetGroupId: 3,
    assetGroupName: 'Mekanik',
    parentAsset: null,
    serialNumber: 'SER-360',
    lastMaintenanceDate: '2026-07-01T00:00:00',
  },
  maintenance: {
    totalWorkOrders: 42,
    openWorkOrders: 2,
    last7Count: 3,
    last30Count: 8,
    last90Count: 20,
    lastWorkOrderDate: '2026-08-24T10:00:00',
  },
  inspectionPriority: {
    score: 62.5,
    level: 'HIGH',
    last7Count: 3,
    last30Count: 8,
    previous30Count: 4,
    last90Count: 20,
    openCount: 2,
    activityChange: 4,
    reasons: ['Son 30 günde 8 iş emri', '2 açık iş emri bulunuyor'],
    analysisWindow: null,
    scoringVersion: 'inspection-priority/v1',
  },
  earlyWarning: {
    score: 48.75,
    level: 'MEDIUM',
    baselineStatus: 'SUFFICIENT',
    last7Count: 3,
    previous7Count: 1,
    last30Count: 8,
    previous30Count: 4,
    last90Count: 20,
    previous90Count: 14,
    baselineMedian: 3,
    baselineMad: 1,
    baselineActiveMonths: 9,
    deviation: 5,
    openCount: 2,
    reasons: ['Son 30 günlük aktivite önceki döneme göre 4 kayıt arttı'],
    components: {
      acceleration: 20,
      shortTermSpike: 10,
      historicalDeviation: 12.5,
      recurrenceBurst: 6.25,
      openEmergence: 0,
    },
    baselineWindow: {
      from: '2025-07-01',
      through: '2026-06-30',
      monthCount: 12,
      minimumActiveMonths: 6,
    },
    scoringVersion: 'early-warning/v1',
  },
  scope: {
    reliability: 'Yellow',
    linkedCanonicalWorkOrders: 162907,
    excludedUnlinkedCanonicalWorkOrders: 8546,
    linkageCoveragePercent: 95.0155,
    historicalWorkOrdersExcluded: true,
    scadaAndOutagesExcluded: true,
    sourceDataset: 'core.Assets + core.WorkOrders',
    notes: [],
  },
  generatedAt: '2026-08-25T12:00:00Z',
}

const assetActivity = {
  assetId: 360,
  items: [
    {
      workOrderId: 54838,
      workOrderNumber: 'WO-54838',
      reportedDateTime: '2026-08-24T10:00:00',
      state: 'OPEN',
      status: 'İşlemde',
      discipline: 'MEKANİK',
      workType: 'ARIZA',
      failureType: 'MEKANİK ARIZA',
      descriptionSnippet: 'Ana pompa hattında titreşim gözlendi.',
      historicalIntervention: {
        requestDescription: 'Pompa titreşimi kontrol edilsin.',
        failureReasonDescription: 'Rulman aşınması',
        workPerformedDescription: 'Rulman değiştirildi ve çalışma kontrolü yapıldı.',
        quality: 'INFORMATIVE',
        observedCompletionDateTime: '2026-08-24T12:30:00',
      },
      interventionCount: 2,
    },
    {
      workOrderId: 54837,
      workOrderNumber: 'WO-54837',
      reportedDateTime: null,
      state: 'CLOSED',
      status: null,
      discipline: null,
      workType: null,
      failureType: null,
      descriptionSnippet: '',
      historicalIntervention: null,
      interventionCount: 0,
    },
  ],
  pageSize: 25,
  hasNextPage: true,
  nextCursor: 'opaque-page-2',
  sourceDataset: 'core.WorkOrders + core.HistoricalInterventions',
  privacyRuleVersion: 'privacy-redaction/email-turkish-mobile/v1',
}

const assetSearchResults = [
  {
    assetId: 651,
    assetCode: '2001KBL00009',
    assetName: 'SCADA Lokasyon Kontrol Çalışması',
    buildingName: 'Merkez Bina',
    locationName: 'Teknik Alan',
    assetGroupName: 'Kontrol Sistemleri',
  },
]

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

const similarCases = {
  metadata: {
    targetWorkOrderId: 54838,
    targetReportedDateTime: '2026-08-02T12:00:00',
    targetAsset: { assetId: 651, assetCode: '20011XZ00112', assetName: 'Giriş LED Aydınlatma' },
    targetDiscipline: 'ELEKTRİK / ELEKTRONİK',
    retrievalMode: 'SAME_ASSET_DISCIPLINE',
    candidateCount: 387,
    returnedCount: 1,
    duplicateTemplatesSuppressed: 2,
    temporalCutoff: '2026-08-02T12:00:00',
    candidateCap: 500,
    algorithmVersion: 'similar-cases/hybrid-jaccard/v1',
    availabilityMessage: null,
  },
  items: [
    {
      workOrderId: 33630,
      workOrderNumber: 'WO-REUSED',
      reportedDateTime: '2026-07-12T09:30:00',
      assetCode: '20011XZ00112',
      assetName: 'Giriş LED Aydınlatma',
      discipline: 'ELEKTRİK / ELEKTRONİK',
      workType: 'ARIZA',
      failureType: 'ELEKTRİK ARIZASI',
      failureReason: null,
      similarityScore: 68.75,
      similarityReasons: ['Açıklama benzerliği %64', 'Aynı varlık', 'Aynı disiplin'],
      descriptionSnippet: 'Giriş bölümündeki LED aydınlatma çalışmıyor.',
      historicalIntervention: {
        requestDescription: 'Giriş aydınlatması çalışmıyor.',
        failureReasonDescription: 'LED sürücü arızası',
        workPerformedDescription: 'LED sürücü değiştirilerek çalışma testi yapıldı.',
        quality: 'INFORMATIVE',
        completionDateTime: '2026-07-12T11:30:00',
      },
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
    ['analytics', 'assets', 360, 'summary'],
    overrides.asset360 ?? asset360,
  )
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
    ['analytics', 'work-orders', 'trend', { assetId: 360, grain: 'Month' }],
    overrides.asset360Trend ?? workOrderTrend,
  )
  client.setQueryData(
    ['analytics', 'assets', 360, 'activity', { cursor: null, pageSize: 25 }],
    overrides.assetActivity ?? assetActivity,
  )
  client.setQueryData(
    ['analytics', 'assets', 'search', { q: '2001KBL00009', limit: 10 }],
    overrides.assetSearch ?? assetSearchResults,
  )
  client.setQueryData(
    ['analytics', 'work-orders', 54838, 'similar-cases', { top: 10 }],
    overrides.similarCases ?? similarCases,
  )
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
      MemoryRouter,
      null,
      React.createElement(
        QueryClientProvider,
        { client: createQueryClient(overrides) },
        React.createElement(Component),
      ),
    ),
  )
}

function renderSimilarCasesPage(overrides, originAssetId = null) {
  return renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      {
        initialEntries: [{
          pathname: '/work-orders/54838/similar-cases',
          state: originAssetId === null ? null : { originAssetId },
        }],
      },
      React.createElement(
        QueryClientProvider,
        { client: createQueryClient(overrides) },
        React.createElement(
          Routes,
          null,
          React.createElement(Route, {
            path: '/work-orders/:id/similar-cases',
            element: React.createElement(SimilarCasesPage),
          }),
        ),
      ),
    ),
  )
}

function renderAssetSearch(overrides) {
  return renderAssetSearchWithClient(createQueryClient(overrides))
}

function renderAssetSearchWithClient(client, initialQuery = '2001KBL00009') {
  return renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      null,
      React.createElement(
        QueryClientProvider,
        { client },
        React.createElement(AssetSearch, { initialQuery }),
      ),
    ),
  )
}

function renderAsset360Page(overrides) {
  return renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: createQueryClient(overrides) },
        React.createElement(
          Routes,
          null,
          React.createElement(Route, {
            path: '/assets/:assetId',
            element: React.createElement(Asset360Page),
          }),
        ),
      ),
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
    await analytics.getAsset360Summary(360)
    await analytics.getAssetActivity(360, { pageSize: 25, cursor: 'opaque-page-2' })
    await analytics.searchAssets({ q: '2001KBL00009', limit: 10 })
    await analytics.getAssetMaintenanceActivityPareto({ dateFrom: '2025-01-01', top: 5 })
    await analytics.getInspectionPriority({ asOf: '2026-08-25', top: 25 })
    await analytics.getEarlyWarning({ asOf: '2026-08-25', top: 10 })
    await analytics.getSimilarCases(54838, { top: 5 })
    await analytics.getWorkOrderActivity({ dateTo: '2025-12-31', discipline: 'MEKANİK' })
    await analytics.getScadaClearanceInterval({ sourceSheet: 'MEKANİK', alarmType: 'SOĞUTMA' })
  } finally {
    analytics.analyticsHttpClient.defaults.adapter = originalAdapter
  }

  assert.deepEqual(requests, [
    {
      url: '/api/analytics/assets/360/summary',
      params: {},
    },
    {
      url: '/api/analytics/assets/360/activity',
      params: { pageSize: 25, cursor: 'opaque-page-2' },
    },
    {
      url: '/api/analytics/assets/search',
      params: { q: '2001KBL00009', limit: 10 },
    },
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
      url: '/api/analytics/work-orders/54838/similar-cases',
      params: { top: 5 },
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
  assert.match(html, /Karar Destek Modülleri/)
  assert.match(html, /Risk ve bakım yoğunluğuna göre önce incelenmesi gereken varlıkları belirleyin/)
  assert.match(html, /Varlıkların kendi normal davranışlarından anlamlı sapmaları takip edin/)
  assert.match(html, /İş emirlerine benzeyen geçmiş vakaları ve gerçekleştirilen müdahaleleri inceleyin/)
  assert.match(html, /href="\/inspection-priority"/)
  assert.match(html, /href="\/early-warning"/)
  assert.match(html, /href="\/work-orders"/)
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
  assert.match(html, /href="\/assets\/651"/)
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

test('Asset 360 route, typed client and hook use canonical numeric AssetId without any', () => {
  const appSource = readFileSync(new URL('../src/App.tsx', import.meta.url), 'utf8')
  const clientSource = readFileSync(new URL('../src/api/analyticsClient.ts', import.meta.url), 'utf8')
  const typesSource = readFileSync(new URL('../src/api/analyticsTypes.ts', import.meta.url), 'utf8')
  const hooksSource = readFileSync(new URL('../src/hooks/useAnalytics.ts', import.meta.url), 'utf8')
  const pageSource = readFileSync(new URL('../src/pages/Asset360Page.tsx', import.meta.url), 'utf8')
  const chartSource = readFileSync(new URL('../src/components/AnalyticsCharts.tsx', import.meta.url), 'utf8')
  const styleSource = readFileSync(new URL('../src/index.css', import.meta.url), 'utf8')

  assert.match(appSource, /path="assets\/:assetId"/)
  assert.match(clientSource, /getAsset360Summary/)
  assert.match(clientSource, /\/api\/analytics\/assets\/\$\{assetId\}\/summary/)
  assert.match(hooksSource, /\['analytics', 'assets', assetId, 'summary'\]/)
  assert.match(chartSource, /reduceTickDensity[\s\S]*maxTicksLimit: 14/)
  assert.match(chartSource, /formatFullMonth\(points\[dataIndex\]\.period\)/)
  assert.match(styleSource, /\.kpi-grid--six \{[\s\S]*repeat\(3, minmax\(0, 1fr\)\)/)
  assert.doesNotMatch([clientSource, typesSource, hooksSource, pageSource].join('\n'), /\bany\b/)
})

test('Phase 2B activity and search contracts are typed, bounded, keyed and cancellation-aware', async () => {
  const clientSource = readFileSync(new URL('../src/api/analyticsClient.ts', import.meta.url), 'utf8')
  const typesSource = readFileSync(new URL('../src/api/analyticsTypes.ts', import.meta.url), 'utf8')
  const hooksSource = readFileSync(new URL('../src/hooks/useAnalytics.ts', import.meta.url), 'utf8')
  const timelineSource = readFileSync(new URL('../src/components/AssetActivityTimeline.tsx', import.meta.url), 'utf8')
  const searchSource = readFileSync(new URL('../src/components/AssetSearch.tsx', import.meta.url), 'utf8')
  const styleSource = readFileSync(new URL('../src/index.css', import.meta.url), 'utf8')

  assert.match(typesSource, /interface AssetActivityResponse[\s\S]*items: AssetActivityItem\[\]/)
  assert.match(typesSource, /interface AssetSearchItem[\s\S]*assetId: number/)
  assert.match(clientSource, /getAssetActivity[\s\S]*\/activity/)
  assert.match(clientSource, /searchAssets[\s\S]*\/api\/analytics\/assets\/search/)
  assert.match(hooksSource, /assetActivityQueryKey[\s\S]*assetId[\s\S]*cursor[\s\S]*pageSize/)
  assert.match(hooksSource, /assetSearchQueryKey[\s\S]*query\.trim\(\)[\s\S]*limit/)
  assert.match(hooksSource, /queryFn: \(\{ signal \}\)[\s\S]*getAssetActivity/)
  assert.match(hooksSource, /queryFn: \(\{ signal \}\)[\s\S]*searchAssets/)
  assert.match(hooksSource, /normalizedQuery\.length >= 2/)
  assert.match(hooksSource, /Math\.min\(Math\.max\(requestedLimit, 1\), 10\)/)
  assert.match(timelineSource, /const activityPageSize = 25/)
  assert.match(timelineSource, /visiblePage\.items\.slice\(0, activityPageSize\)/)
  assert.match(timelineSource, /slice\(-maximumCursorHistory\)/)
  assert.match(searchSource, /const searchResultLimit = 10/)
  assert.match(searchSource, /const searchDebounceMilliseconds = 300/)
  assert.match(styleSource, /\.asset-activity-item__summary \{[\s\S]*grid-template-columns: minmax\(0, 1fr\) minmax\(150px, auto\)/)
  assert.match(styleSource, /@media \(max-width: 680px\)[\s\S]*\.asset-activity-item__summary \{ grid-template-columns: 1fr/)
  const activityContractSource = typesSource.slice(
    typesSource.indexOf('export type AssetActivityState'),
    typesSource.indexOf('export interface AssetMaintenanceActivityParetoItem'),
  )
  assert.doesNotMatch(
    [activityContractSource, timelineSource, searchSource].join('\n'),
    /\bany\b|descriptionRaw|requestDescriptionRaw|workPerformedDescriptionRaw|requestedByName|assignedPersonnelName|sourceFileName|sourceSheet|sourceRowNumber|fingerprint/i,
  )

  const originalAdapter = analytics.analyticsHttpClient.defaults.adapter
  const controller = new AbortController()
  const signals = []
  analytics.analyticsHttpClient.defaults.adapter = async (config) => {
    signals.push(config.signal)
    return { data: {}, status: 200, statusText: 'OK', headers: {}, config }
  }
  try {
    await analytics.getAssetActivity(360, { pageSize: 25 }, controller.signal)
    await analytics.searchAssets({ q: 'A-360', limit: 10 }, controller.signal)
  } finally {
    analytics.analyticsHttpClient.defaults.adapter = originalAdapter
  }
  assert.deepEqual(signals, [controller.signal, controller.signal])
})

test('Asset 360 renders one privacy-safe activity page in the required section order', () => {
  const html = renderAsset360Page()
  const trendIndex = html.indexOf('Aylık İş Emri Trendi')
  const activityIndex = html.indexOf('Varlık İş Emri Geçmişi')
  const scopeIndex = html.indexOf('Bağlantı ve Güvenilirlik Notu')

  assert.ok(trendIndex >= 0 && activityIndex > trendIndex && scopeIndex > activityIndex)
  assert.match(html, /Sayfa 1 · En fazla 25 kayıt/)
  assert.match(html, /WO-54838/)
  assert.match(html, />AÇIK</)
  assert.match(html, />KAPALI</)
  assert.match(html, /Ana pompa hattında titreşim gözlendi/)
  assert.match(html, /aria-expanded="false"/)
  assert.match(html, /aria-controls="asset-360-work-order-54838-intervention"/)
  assert.match(html, /Gözlenen müdahale tamamlanma zamanı/)
  assert.match(html, /Rulman değiştirildi ve çalışma kontrolü yapıldı/)
  assert.match(html, /Eşleşen müdahale kaydı/)
  assert.match(html, /Geçmiş müdahale kaydı bulunmuyor/)
  assert.match(html, /href="\/work-orders\/54838\/similar-cases"/)
  assert.match(html, />Önceki</)
  assert.match(html, />Sonraki</)
  assert.match(html, /Kayıtlar sayfalar halinde gösterilir/)
  assert.doesNotMatch(html, /cursor sayfası|Canonical bakım aktivitesi/i)
  assert.doesNotMatch(html, />null<|descriptionRaw|requestedByName|assignedPersonnelName|sourceFileName|fingerprint/i)
})

test('Asset activity handles empty, loading and retryable failure without replacing Phase 1 summary', () => {
  const emptyHtml = renderAsset360Page({
    assetActivity: { ...assetActivity, items: [], hasNextPage: false, nextCursor: null },
  })
  assert.match(emptyHtml, /Bu varlıkla eşleşen güncel iş emri bulunmuyor/)
  assert.match(emptyHtml, /Toplam İş Emri/)

  const pendingClient = createQueryClient()
  pendingClient.removeQueries({
    queryKey: ['analytics', 'assets', 360, 'activity', { cursor: null, pageSize: 25 }],
    exact: true,
  })
  const pendingHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: pendingClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(pendingHtml, /Varlık iş emri geçmişi yükleniyor/)
  assert.match(pendingHtml, /Toplam İş Emri/)

  const errorClient = createQueryClient()
  errorClient.setDefaultOptions({
    queries: { retry: false, retryOnMount: false, refetchOnMount: false, staleTime: Infinity },
  })
  const errorQuery = errorClient.getQueryCache().find({
    queryKey: ['analytics', 'assets', 360, 'activity', { cursor: null, pageSize: 25 }],
    exact: true,
  })
  errorQuery.setState({
    ...errorQuery.state,
    status: 'error',
    fetchStatus: 'idle',
    data: undefined,
    error: new Error('Timeline servisi kullanılamıyor'),
  })
  const errorHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: errorClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(errorHtml, /Timeline servisi kullanılamıyor/)
  assert.match(errorHtml, /Yeniden dene/)
  assert.match(errorHtml, /Aylık İş Emri Trendi/)
})

test('Asset activity cursor source preserves rows on page error and resets stale snapshots safely', () => {
  const source = readFileSync(new URL('../src/components/AssetActivityTimeline.tsx', import.meta.url), 'utf8')

  assert.match(source, /const \[visiblePage, setVisiblePage\]/)
  assert.match(source, /visiblePage && activity\.error/)
  assert.match(source, /Mevcut kayıtlar korunuyor/)
  assert.match(source, /activity\.error instanceof AnalyticsApiError && activity\.error\.status === 409/)
  assert.match(source, /setCursorHistory\(\[\{ cursor: null, pageNumber: 1 \}\]\)/)
  assert.match(source, /Veri seti yenilendi/)
  assert.match(source, /cursorHistory\.length <= 1/)
  assert.match(source, /visiblePage\.nextCursor/)
  assert.doesNotMatch(source, /useInfiniteQuery|fetchNextPage|flatMap/)
})

test('Assets page search renders bounded numeric links and preserves portfolio content', () => {
  const searchHtml = renderAssetSearch()
  const assetsHtml = renderPage(AssetsPage)

  assert.match(searchHtml, /Varlık Ara/)
  assert.match(searchHtml, /Varlık keşfi/i)
  assert.doesNotMatch(searchHtml, /Canonical varlık keşfi/i)
  assert.match(searchHtml, /Varlık kodu veya adıyla ara/)
  assert.match(searchHtml, /En fazla 10 sonuç/)
  assert.match(searchHtml, /2001KBL00009/)
  assert.match(searchHtml, /SCADA Lokasyon Kontrol Çalışması/)
  assert.match(searchHtml, /Merkez Bina · Teknik Alan · Kontrol Sistemleri/)
  assert.match(searchHtml, /href="\/assets\/651"/)
  assert.match(assetsHtml, /Varlık Ara/)
  assert.match(assetsHtml, /Toplam Varlık/)
  assert.match(assetsHtml, /İş Emri Aktivitesi En Yoğun Asset/)
  assert.doesNotMatch(searchHtml, /serialNumber|fingerprint|sourceFile|AssetIdRaw/i)
})

test('Asset search handles helper, loading, error, no-result and clear/debounce contracts', () => {
  const helperHtml = renderAssetSearchWithClient(createQueryClient(), 'A')
  assert.match(helperHtml, /Arama yapmak için en az 2 karakter girin/)
  assert.doesNotMatch(helperHtml, /Varlıklar aranıyor/)

  const loadingClient = createQueryClient()
  loadingClient.removeQueries({
    queryKey: ['analytics', 'assets', 'search', { q: '2001KBL00009', limit: 10 }],
    exact: true,
  })
  assert.match(renderAssetSearchWithClient(loadingClient), /Varlıklar aranıyor/)

  const errorClient = createQueryClient()
  errorClient.setDefaultOptions({
    queries: { retry: false, retryOnMount: false, refetchOnMount: false, staleTime: Infinity },
  })
  const errorQuery = errorClient.getQueryCache().find({
    queryKey: ['analytics', 'assets', 'search', { q: '2001KBL00009', limit: 10 }],
    exact: true,
  })
  errorQuery.setState({
    ...errorQuery.state,
    status: 'error',
    fetchStatus: 'idle',
    data: undefined,
    error: new Error('Arama servisi kullanılamıyor'),
  })
  const errorHtml = renderAssetSearchWithClient(errorClient)
  assert.match(errorHtml, /Arama sonuçları alınamadı/)
  assert.match(errorHtml, /Yeniden dene/)

  const noResultHtml = renderAssetSearch({ assetSearch: [] })
  assert.match(noResultHtml, /Aramanızla eşleşen varlık bulunamadı/)

  const source = readFileSync(new URL('../src/components/AssetSearch.tsx', import.meta.url), 'utf8')
  assert.match(source, /normalizedInput\.length >= 2/)
  assert.match(source, /window\.setTimeout/)
  assert.match(source, /setDebouncedQuery\(''\)/)
  assert.match(source, /setInput\(''\)/)
  assert.match(source, /disabled=\{!canSearch/)
})

test('Asset 360 renders identity, KPIs, explainable decisions, trend and scope safeguards', () => {
  const html = renderAsset360Page()

  assert.match(html, /Ana Soğutma Pompası/)
  assert.match(html, /Varlık 360/)
  assert.match(html, /A-360/)
  assert.match(html, /A Blok → Mekanik Oda → Mekanik/)
  assert.match(html, /Toplam İş Emri/)
  assert.match(html, /Açık İş Emri/)
  assert.match(html, /Son 7 Gün/)
  assert.match(html, /Son 30 Gün/)
  assert.match(html, /Son 90 Gün/)
  assert.match(html, /24 Ağu 2026/)
  assert.match(html, /YÜKSEK · Öncelikli inceleme/)
  assert.match(html, /62,5/)
  assert.match(html, /Son 30 günde 8 iş emri/)
  assert.match(html, /arıza olasılığı değildir/)
  assert.match(html, /ORTA · İzle/)
  assert.match(html, /48,75/)
  assert.match(html, /Median \/ MAD/)
  assert.match(html, /Puan katkıları/)
  assert.match(html, /İnceleme Önceliği bakım iş yüküne göre hangi varlıklara önce bakılması gerektiğini/)
  assert.match(html, /iki sonuç farklı seviyelerde olabilir/)
  assert.match(html, /Aylık İş Emri Trendi/)
  assert.match(html, /Yalnızca bu varlıkla doğrulanmış şekilde eşleşen güncel iş emirleri/)
  assert.match(html, /DOĞRULANMIŞ/)
  assert.match(html, /VERİ KALİTESİ NOTU/)
  assert.match(html, /162\.907/)
  assert.match(html, /8\.546/)
  assert.match(html, /95,02%/)
  assert.match(html, /Varlıkla eşleşen kayıt/)
  assert.match(html, /Varlıkla eşleşmeyen kayıt/)
  assert.match(html, /Eşleşme oranı/)
  assert.match(html, /Doğrulanmış varlık ve güncel iş emri verileri/)
  assert.match(html, /Eski tarihsel veri seti güncel analiz sonuçlarına dahil edilmez/)
  assert.match(html, /SCADA ve outage verileri gösterilmez/)
  assert.match(html, /Varlık kaydındaki son bakım/)
  assert.match(html, /Son kayıtlı iş emri/)
  assert.doesNotMatch(html, />HIGH ·|>MEDIUM ·|>LOW ·|>GREEN ·|>YELLOW ·/)
  assert.doesNotMatch(html, /failure probability|predictive failure|health score|MTTR|raw intervention/i)
})

test('Asset 360 preserves zero-activity and insufficient-baseline states', () => {
  const html = renderAsset360Page({
    asset360: {
      ...asset360,
      maintenance: {
        totalWorkOrders: 0,
        openWorkOrders: 0,
        last7Count: 0,
        last30Count: 0,
        last90Count: 0,
        lastWorkOrderDate: null,
      },
      inspectionPriority: {
        ...asset360.inspectionPriority,
        score: 0,
        level: 'LOW',
        last7Count: 0,
        last30Count: 0,
        previous30Count: 0,
        last90Count: 0,
        openCount: 0,
        activityChange: 0,
        reasons: [],
      },
      earlyWarning: {
        ...asset360.earlyWarning,
        score: null,
        level: null,
        baselineStatus: 'INSUFFICIENT_BASELINE',
        baselineMedian: null,
        baselineMad: null,
        baselineActiveMonths: 0,
        deviation: null,
        components: null,
        reasons: ['12 aylık baseline içinde en az 6 aktif ay gerekir; 0 aktif ay bulundu'],
      },
    },
    asset360Trend: {
      ...workOrderTrend,
      points: [],
      metadata: { ...workOrderTrend.metadata, matchedRecordCount: 0, validRecordCount: 0 },
    },
  })

  assert.match(html, /DÜŞÜK · Düşük öncelik/)
  assert.match(html, /İnceleme önceliğini yükselten aktivite sinyali yok/)
  assert.match(html, /YETERSİZ GEÇMİŞ VERİ/)
  assert.match(html, /Yetersiz geçmiş veri/)
  assert.match(html, /Bilgi bulunmuyor/)
  assert.match(html, /Seçilen filtrelerle eşleşen kayıt bulunamadı/)
})

test('Asset 360 renders loading, retryable error, deterministic 404 and invalid-route states', () => {
  const pendingClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const pendingHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: pendingClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(pendingHtml, /Asset 360 özeti hazırlanıyor/)

  const errorClient = createQueryClient()
  const errorQuery = errorClient.getQueryCache().find({
    queryKey: ['analytics', 'assets', 360, 'summary'],
    exact: true,
  })
  errorQuery.setState({
    ...errorQuery.state,
    status: 'error',
    fetchStatus: 'idle',
    error: new Error('Asset özeti kullanılamıyor'),
  })
  const errorHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: errorClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(errorHtml, /Asset özeti kullanılamıyor/)
  assert.match(errorHtml, /Yeniden dene/)

  const notFoundClient = createQueryClient()
  const notFoundQuery = notFoundClient.getQueryCache().find({
    queryKey: ['analytics', 'assets', 360, 'summary'],
    exact: true,
  })
  notFoundQuery.setState({
    ...notFoundQuery.state,
    status: 'error',
    fetchStatus: 'idle',
    error: new analytics.AnalyticsApiError('Canonical asset not found.', 404),
  })
  const notFoundHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/360'] },
      React.createElement(
        QueryClientProvider,
        { client: notFoundClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(notFoundHtml, /Varlık bulunamadı/)
  assert.match(notFoundHtml, /doğrulanmış varlık bulunamadı/)

  const invalidHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/assets/not-a-number'] },
      React.createElement(
        QueryClientProvider,
        { client: new QueryClient() },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/assets/:assetId',
          element: React.createElement(Asset360Page),
        })),
      ),
    ),
  )
  assert.match(invalidHtml, /Geçerli bir varlık kimliği belirtilmedi/)
})

test('inspection priority renders ranked signals, reasons, coverage and safe semantics', () => {
  const html = renderPage(InspectionPriorityPage)

  assert.match(html, /İnceleme Önceliği/)
  assert.match(html, /ASSET-HIGH/)
  assert.match(html, /href="\/assets\/1"/)
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
  assert.match(html, /Öncelik dağılımı/)
  assert.match(html, /Seçili Top-10 kapsamı/)
  assert.match(html, /signal-summary--priority/)
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
  assert.match(html, /href="\/assets\/1"/)
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
  assert.match(html, /Uyarı dağılımı/)
  assert.match(html, /signal-summary--warning/)
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
  assert.match(html, /Karar destek/)
  assert.match(html, /Veri ve denetim/)
  assert.match(html, /sidebar-link__icon/)
  assert.match(html, /alt="Gürsan Teknik Hizmetler"/)
  assert.match(html, /gursan-logo-red-white\.png/)
  assert.doesNotMatch(html, /brand__logo-surface/)
  assert.match(html, /Bakım &amp; Güvenilirlik/)
})

test('official local Gürsan symbol favicons are configured without a runtime hotlink', () => {
  const indexHtml = readFileSync(new URL('../index.html', import.meta.url), 'utf8')

  assert.match(indexHtml, /href="\/favicon\.ico\?v=2"/)
  assert.match(indexHtml, /href="\/favicon-48x48\.png\?v=2"/)
  assert.match(indexHtml, /href="\/favicon-32x32\.png\?v=2"/)
  assert.match(indexHtml, /href="\/favicon-16x16\.png\?v=2"/)
  assert.doesNotMatch(indexHtml, /gursanteknik\.com/)
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
  assert.match(html, /Benzer Geçmiş Vakalar/)
  assert.match(html, /Canonical WorkOrder ID/)
  assert.match(html, /Benzer Vakaları Gör/)
  assert.match(html, /çözüm önerisi üretmez/i)
  assert.doesNotMatch(html, /Güncel İş Emri|Geçmiş İş Emri Aktivitesi/)
})

test('similar historical cases render bounded evidence without solution or personnel claims', () => {
  const html = renderSimilarCasesPage()

  assert.match(html, /Benzer Geçmiş Vakalar/)
  assert.match(html, /Seçilen iş emrine benzer geçmiş bakım\/talep kayıtlarını gösterir/)
  assert.match(html, /Seçilen İş Emri/)
  assert.match(html, /Benzer Geçmiş Vaka Sonuçları/)
  assert.match(html, /Benzerlik 68,75%/)
  assert.match(html, /Giriş bölümündeki LED aydınlatma çalışmıyor/)
  assert.match(html, /Aynı varlık/)
  assert.match(html, /Aynı disiplin/)
  assert.match(html, /Aynı varlık \+ aynı disiplin/)
  assert.match(html, /Değerlendirilen aday<\/span><strong>387/)
  assert.match(html, /Tekrarlayan template bastırıldı<\/span><strong>2/)
  assert.match(html, /core\.WorkOrders/)
  assert.match(html, /core\.HistoricalInterventions/)
  assert.match(html, /Geçmiş Vakada Yapılan İşlem/)
  assert.match(html, /Kayıt kalitesi: Bilgilendirici/)
  assert.match(html, /LED sürücü değiştirilerek çalışma testi yapıldı/)
  assert.match(html, /LED sürücü arızası/)
  assert.match(html, /12 Tem 2026 11:30/)
  assert.match(html, /Requester ve sorumlu personel alanları gösterilmez/)
  assert.match(html, /çözüm önerisi veya otomatik bakım talimatı değildir/)
  assert.doesNotMatch(html, /failure probability|çözüm adımı|önerilen çözüm|RequestedBy|AssignedPerson|dangerouslySetInnerHTML/i)
})

test('Similar Cases uses validated Asset 360 origin navigation and preserves fallback', () => {
  const fromAssetHtml = renderSimilarCasesPage(undefined, 360)
  const fallbackHtml = renderSimilarCasesPage()
  const invalidOriginHtml = renderSimilarCasesPage(undefined, '/assets/999')
  const timelineSource = readFileSync(new URL('../src/components/AssetActivityTimeline.tsx', import.meta.url), 'utf8')
  const similarSource = readFileSync(new URL('../src/pages/SimilarCasesPage.tsx', import.meta.url), 'utf8')

  assert.match(fromAssetHtml, /href="\/assets\/360"/)
  assert.match(fromAssetHtml, /Varlık 360’a dön/)
  assert.doesNotMatch(fromAssetHtml, /İş Emirlerine dön/)
  assert.match(fallbackHtml, /href="\/work-orders"/)
  assert.match(fallbackHtml, /İş Emirlerine dön/)
  assert.match(invalidOriginHtml, /href="\/work-orders"/)
  assert.doesNotMatch(invalidOriginHtml, /href="\/assets\/999"/)
  assert.match(timelineSource, /state=\{\{ originAssetId: assetId \}\}/)
  assert.match(timelineSource, /to=\{`\/work-orders\/\$\{item\.workOrderId\}\/similar-cases`\}/)
  assert.match(similarSource, /Number\.isSafeInteger\(parsed\) && parsed > 0/)
  assert.doesNotMatch(similarSource, /location\.state.*to=|window\.location|navigate\(location\.state/)
})

test('similar historical cases render generic, no-action and missing intervention states safely', () => {
  const baseItem = similarCases.items[0]
  const html = renderSimilarCasesPage({
    similarCases: {
      ...similarCases,
      metadata: { ...similarCases.metadata, returnedCount: 3 },
      items: [
        {
          ...baseItem,
          workOrderId: 1,
          historicalIntervention: {
            requestDescription: null,
            failureReasonDescription: null,
            workPerformedDescription: 'Genel kontrol gerçekleştirildi.',
            quality: 'GENERIC',
            completionDateTime: null,
          },
        },
        {
          ...baseItem,
          workOrderId: 2,
          historicalIntervention: {
            requestDescription: null,
            failureReasonDescription: null,
            workPerformedDescription: null,
            quality: 'NO_ACTION',
            completionDateTime: null,
          },
        },
        { ...baseItem, workOrderId: 3, historicalIntervention: null },
      ],
    },
  })

  assert.match(html, /Kayıt kalitesi: Genel/)
  assert.match(html, /Genel kontrol gerçekleştirildi/)
  assert.match(html, /Bu geçmiş kayıt için anlamlı müdahale açıklaması bulunmuyor/)
  assert.match(html, /Bu geçmiş vaka için müdahale verisi bulunamadı/)
  assert.doesNotMatch(html, /Önerilen Çözüm|Kesin Çözüm|AI Önerisi|MTTR/i)
})

test('similar historical intervention content is escaped as plain React text', () => {
  const html = renderSimilarCasesPage({
    similarCases: {
      ...similarCases,
      items: [{
        ...similarCases.items[0],
        historicalIntervention: {
          ...similarCases.items[0].historicalIntervention,
          workPerformedDescription: '<b>Gözlenen işlem</b>',
          requestDescriptionRaw: 'PRIVATE-REQUEST-RAW',
          workPerformedDescriptionRaw: 'PRIVATE-ACTION-RAW',
          sourceFileName: 'PRIVATE-SOURCE-PATH',
        },
      }],
    },
  })

  assert.match(html, /&lt;b&gt;Gözlenen işlem&lt;\/b&gt;/)
  assert.doesNotMatch(html, /<b>Gözlenen işlem<\/b>/)
  assert.doesNotMatch(html, /PRIVATE-REQUEST-RAW|PRIVATE-ACTION-RAW|PRIVATE-SOURCE-PATH/)
})

test('similar historical cases render empty, loading, error and retry states', () => {
  const emptyHtml = renderSimilarCasesPage({
    similarCases: {
      metadata: {
        ...similarCases.metadata,
        retrievalMode: 'NOT_AVAILABLE',
        candidateCount: 0,
        returnedCount: 0,
        duplicateTemplatesSuppressed: 0,
        availabilityMessage: 'Target WorkOrder has no linked AssetId.',
      },
      items: [],
    },
  })
  assert.match(emptyHtml, /bir varlığa bağlı olmadığı için benzer vaka analizi kullanılamıyor/)

  const pendingClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const pendingHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/work-orders/54838/similar-cases'] },
      React.createElement(
        QueryClientProvider,
        { client: pendingClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/work-orders/:id/similar-cases',
          element: React.createElement(SimilarCasesPage),
        })),
      ),
    ),
  )
  assert.match(pendingHtml, /Benzer geçmiş vakalar aranıyor/)

  const errorClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, retryOnMount: false, refetchOnMount: false, staleTime: Infinity },
    },
  })
  const errorQuery = errorClient.getQueryCache().build(errorClient, {
    queryKey: ['analytics', 'work-orders', 54838, 'similar-cases', { top: 10 }],
    queryFn: async () => similarCases,
  })
  errorQuery.setState({ ...errorQuery.state, status: 'error', fetchStatus: 'idle', error: new Error('Benzer vaka servisi kullanılamıyor') })
  const errorHtml = renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: ['/work-orders/54838/similar-cases'] },
      React.createElement(
        QueryClientProvider,
        { client: errorClient },
        React.createElement(Routes, null, React.createElement(Route, {
          path: '/work-orders/:id/similar-cases',
          element: React.createElement(SimilarCasesPage),
        })),
      ),
    ),
  )
  assert.match(errorHtml, /Benzer vaka servisi kullanılamıyor/)
  assert.match(errorHtml, /Yeniden dene/)
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
