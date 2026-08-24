import { useState, type FormEvent } from 'react'
import type { ScadaAnalyticsQuery } from '../api/analyticsTypes'
import { ChartPanel, HorizontalBarChart, TrendLineChart } from '../components/AnalyticsCharts'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { ScadaClearanceInterval } from '../components/ScadaClearanceInterval'
import { useScadaOverview, useScadaTrend } from '../hooks/useAnalytics'
import { formatCount } from '../utils/format'

export function ScadaPage() {
  const [draft, setDraft] = useState<ScadaAnalyticsQuery>({})
  const [filters, setFilters] = useState<ScadaAnalyticsQuery>({})
  const [filterError, setFilterError] = useState('')
  const optionsQuery = useScadaOverview()
  const overview = useScadaOverview(filters)
  const trend = useScadaTrend({ ...filters, grain: 'Month' })

  const applyFilters = (event: FormEvent) => {
    event.preventDefault()
    if (draft.dateFrom && draft.dateTo && draft.dateFrom > draft.dateTo) {
      setFilterError('Başlangıç tarihi bitiş tarihinden sonra olamaz.')
      return
    }

    setFilterError('')
    setFilters({ ...draft })
  }

  const resetFilters = () => {
    setDraft({})
    setFilters({})
    setFilterError('')
  }

  const isPending = optionsQuery.isPending || overview.isPending || trend.isPending
  const error = optionsQuery.error ?? overview.error ?? trend.error

  if (isPending) {
    return <LoadingState label="SCADA analitiği yükleniyor" />
  }

  if (error) {
    return (
      <ErrorState
        error={error}
        onRetry={() => {
          void optionsQuery.refetch()
          void overview.refetch()
          void trend.refetch()
        }}
      />
    )
  }

  if (!optionsQuery.data || !overview.data || !trend.data) {
    return <EmptyState />
  }

  const data = overview.data

  return (
    <div className="page-stack">
      <PageHeader
        eyebrow="SCADA kaynak kayıtları"
        title="SCADA Operasyon Görünümü"
        description="Kaynak occurrence dağılımları ve güvenilir timestamp tabanlı aylık eğilim."
        actions={<DataTimestamp value={data.metadata.dataAsOf} />}
      />

      <form className="filter-panel" onSubmit={applyFilters} aria-label="SCADA filtreleri">
        <div className="filter-grid filter-grid--three">
          <label>
            <span>Başlangıç tarihi</span>
            <input className="form-control" type="date" value={draft.dateFrom ?? ''} onChange={(event) => setDraft((current) => ({ ...current, dateFrom: event.target.value || undefined }))} />
          </label>
          <label>
            <span>Bitiş tarihi</span>
            <input className="form-control" type="date" value={draft.dateTo ?? ''} onChange={(event) => setDraft((current) => ({ ...current, dateTo: event.target.value || undefined }))} />
          </label>
          <label>
            <span>Kaynak sayfa</span>
            <select className="form-select" value={draft.sourceSheet ?? ''} onChange={(event) => setDraft((current) => ({ ...current, sourceSheet: event.target.value || undefined }))}>
              <option value="">Tümü</option>
              {optionsQuery.data.bySourceSheet.map((item) => <option key={item.category} value={item.category}>{item.category}</option>)}
            </select>
          </label>
        </div>
        {filterError ? <p className="filter-error" role="alert">{filterError}</p> : null}
        <div className="filter-actions">
          <button className="btn btn-primary" type="submit">Uygula</button>
          <button className="btn btn-outline-secondary" type="button" onClick={resetFilters}>Temizle</button>
        </div>
      </form>

      {data.totalAlarmOccurrences === 0 ? (
        <EmptyState />
      ) : (
        <>
          <section className="kpi-grid" aria-label="SCADA göstergeleri">
            <KpiCard label="Toplam Alarm Kaydı" value={data.totalAlarmOccurrences} note="Kaynak satır/occurrence sayısı" reliability={data.metadata.reliability} />
            <KpiCard label="Timestamp Kalite Sorunu" value={data.invalidOrMissingTimestampCount} reliability={data.metadata.reliability} tone="amber" />
            <KpiCard label="Tarih Kalitesi Bulgusu" value={data.dateQualityIssueCount} reliability={data.metadata.reliability} tone="slate" />
            <KpiCard label="Trendde Kullanılan Kayıt" value={trend.data.quality.validRecordCount} reliability={trend.data.metadata.reliability} tone="teal" />
          </section>

          <ChartPanel title="Aylık Alarm Kaydı Trendi" subtitle="Yalnız güvenilir ReceivedAt kayıtları" reliability={trend.data.metadata.reliability}>
            <TrendLineChart points={trend.data.points} />
            <div className="quality-summary" aria-label="SCADA trend veri kalitesi özeti">
              <span>Eşleşen <strong>{formatCount(trend.data.metadata.matchedRecordCount)}</strong></span>
              <span>Geçerli <strong>{formatCount(trend.data.quality.validRecordCount)}</strong></span>
              <span>Kalite nedeniyle dışlanan <strong>{formatCount(trend.data.quality.excludedByQualityCount)}</strong></span>
            </div>
          </ChartPanel>

          <div className="dashboard-grid dashboard-grid--two">
            <ChartPanel title="Kaynak Bazında Alarm" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.bySourceSheet.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Alarm Tipi" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byAlarmType.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Müdahale Seviyesi" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byInterventionLevel.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Bölüm Dağılımı" reliability={data.bySectionReliability}>
              <HorizontalBarChart data={data.bySection.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
          </div>
        </>
      )}

      <ScadaClearanceInterval query={filters} />

      <InfoNote>Alarm kaydı, kaynak sistemdeki satır/occurrence sayısını temsil eder; benzersiz fiziksel olay iddiası taşımaz.</InfoNote>
    </div>
  )
}
