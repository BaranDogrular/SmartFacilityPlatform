import { useState, type FormEvent } from 'react'
import type { WorkOrderActivityQuery } from '../api/analyticsTypes'
import { useWorkOrderActivity } from '../hooks/useAnalytics'
import { ChartPanel, HorizontalBarChart, TrendLineChart } from './AnalyticsCharts'
import { EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, ReliabilityBadge } from './DashboardUi'

const emptyFilters: WorkOrderActivityQuery = {}

export function WorkOrderActivity() {
  const [draft, setDraft] = useState<WorkOrderActivityQuery>(emptyFilters)
  const [filters, setFilters] = useState<WorkOrderActivityQuery>(emptyFilters)
  const [filterError, setFilterError] = useState('')
  const optionsQuery = useWorkOrderActivity()
  const activityQuery = useWorkOrderActivity(filters)

  const applyFilters = (event: FormEvent) => {
    event.preventDefault()
    if (draft.dateFrom && draft.dateTo && draft.dateFrom > draft.dateTo) {
      setFilterError('Başlangıç tarihi bitiş tarihinden sonra olamaz.')
      return
    }

    setFilterError('')
    setFilters({ ...draft })
  }

  const clearFilters = () => {
    setDraft({})
    setFilters({})
    setFilterError('')
  }

  const isPending = optionsQuery.isPending || activityQuery.isPending
  const error = optionsQuery.error ?? activityQuery.error
  const data = activityQuery.data

  return (
    <section className="analytics-section" aria-labelledby="work-order-activity-title">
      <header className="analytics-section__header">
        <div>
          <p className="page-eyebrow">Canonical tarihsel görünüm</p>
          <h2 id="work-order-activity-title">İş Emri Aktivitesi</h2>
          <p>Canonical WorkOrder kayıtlarının tarih trendi ve raw disiplin dağılımı.</p>
        </div>
        {data ? <ReliabilityBadge reliability={data.metadata.reliability} /> : null}
      </header>

      <form className="filter-panel" onSubmit={applyFilters} aria-label="İş emri aktivite filtreleri">
        <div className="filter-grid filter-grid--three">
          <label>
            <span>Başlangıç tarihi</span>
            <input
              className="form-control"
              name="activityDateFrom"
              type="date"
              value={draft.dateFrom ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateFrom: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Bitiş tarihi</span>
            <input
              className="form-control"
              name="activityDateTo"
              type="date"
              value={draft.dateTo ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateTo: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Disiplin</span>
            <select
              className="form-select"
              name="activityDiscipline"
              value={draft.discipline ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, discipline: event.target.value || undefined }))}
            >
              <option value="">Tümü</option>
              {(optionsQuery.data?.byDiscipline ?? []).map((item) => (
                <option key={item.category} value={item.category}>{item.category}</option>
              ))}
            </select>
          </label>
        </div>
        {filterError ? <p className="filter-error" role="alert">{filterError}</p> : null}
        <div className="filter-actions">
          <button className="btn btn-primary" type="submit">Uygula</button>
          <button className="btn btn-outline-secondary" type="button" onClick={clearFilters}>Temizle</button>
        </div>
      </form>

      {isPending ? <LoadingState label="İş emri aktivitesi yükleniyor" /> : null}
      {error ? (
        <ErrorState error={error} onRetry={() => {
          void optionsQuery.refetch()
          void activityQuery.refetch()
        }} />
      ) : null}
      {!isPending && !error && !data ? <EmptyState /> : null}
      {!isPending && !error && data ? (
        data.metadata.matchedRecordCount === 0 ? (
          <EmptyState message="Seçilen filtrelerle eşleşen iş emri kaydı bulunamadı." />
        ) : (
          <>
            <section className="kpi-grid kpi-grid--one" aria-label="İş emri aktivite göstergeleri">
              <KpiCard label="Eşleşen İş Emri" value={data.metadata.matchedRecordCount} reliability={data.metadata.reliability} tone="teal" />
            </section>
            <div className="dashboard-grid dashboard-grid--two">
              <ChartPanel title="Aylık Aktivite Trendi" subtitle="Canonical ReportedDateTime alanına göre" reliability={data.metadata.reliability}>
                <TrendLineChart points={data.trend} />
              </ChartPanel>
              <ChartPanel title="Disiplin Dağılımı" subtitle={data.appliedDiscipline ? `Uygulanan disiplin: ${data.appliedDiscipline}` : 'Raw kaynak değerleri'} reliability={data.metadata.reliability}>
                <HorizontalBarChart data={data.byDiscipline.map((item) => ({ label: item.category, count: item.count }))} />
              </ChartPanel>
            </div>
          </>
        )
      ) : null}

      <InfoNote>
        Kaynak <span className="code-chip">core.WorkOrders</span> canonical snapshot'tır.
        Legacy dated snapshot bu analitiğe dahil edilmez.
      </InfoNote>
    </section>
  )
}
