import { useState, type FormEvent } from 'react'
import type { HistoricalMaintenanceActivityQuery } from '../api/analyticsTypes'
import { useHistoricalMaintenanceActivity } from '../hooks/useAnalytics'
import { ChartPanel, HorizontalBarChart, TrendLineChart } from './AnalyticsCharts'
import { EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, ReliabilityBadge } from './DashboardUi'

const emptyFilters: HistoricalMaintenanceActivityQuery = {}

export function HistoricalMaintenanceActivity() {
  const [draft, setDraft] = useState<HistoricalMaintenanceActivityQuery>(emptyFilters)
  const [filters, setFilters] = useState<HistoricalMaintenanceActivityQuery>(emptyFilters)
  const [filterError, setFilterError] = useState('')
  const optionsQuery = useHistoricalMaintenanceActivity()
  const activityQuery = useHistoricalMaintenanceActivity(filters)

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
    <section className="analytics-section analytics-section--historical" aria-labelledby="historical-activity-title">
      <header className="analytics-section__header">
        <div>
          <p className="page-eyebrow">Ayrı historical dataset</p>
          <h2 id="historical-activity-title">Geçmiş İş Emri Aktivitesi</h2>
          <p>Historical kayıt trendi ve raw Discipline dağılımı; current WorkOrder verisi dahil değildir.</p>
        </div>
        {data ? <ReliabilityBadge reliability={data.metadata.reliability} /> : null}
      </header>

      <form className="filter-panel" onSubmit={applyFilters} aria-label="Historical iş emri filtreleri">
        <div className="filter-grid filter-grid--three">
          <label>
            <span>Başlangıç tarihi</span>
            <input
              className="form-control"
              name="historicalDateFrom"
              type="date"
              value={draft.dateFrom ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateFrom: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Bitiş tarihi</span>
            <input
              className="form-control"
              name="historicalDateTo"
              type="date"
              value={draft.dateTo ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateTo: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Raw Discipline</span>
            <select
              className="form-select"
              name="historicalDiscipline"
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

      {isPending ? <LoadingState label="Historical iş emri aktivitesi yükleniyor" /> : null}
      {error ? (
        <ErrorState
          error={error}
          onRetry={() => {
            void optionsQuery.refetch()
            void activityQuery.refetch()
          }}
        />
      ) : null}
      {!isPending && !error && !data ? <EmptyState /> : null}
      {!isPending && !error && data ? (
        data.metadata.matchedRecordCount === 0 ? (
          <EmptyState message="Seçilen historical filtrelerle eşleşen kayıt bulunamadı." />
        ) : (
          <>
            <section className="kpi-grid kpi-grid--one" aria-label="Historical iş emri göstergeleri">
              <KpiCard
                label="Eşleşen Historical Kayıt"
                value={data.metadata.matchedRecordCount}
                reliability={data.metadata.reliability}
                tone="teal"
              />
            </section>

            <div className="dashboard-grid dashboard-grid--two">
              <ChartPanel
                title="Historical Aylık Aktivite Trendi"
                subtitle="Historical ReportedDateTime alanına göre"
                reliability={data.metadata.reliability}
              >
                <TrendLineChart points={data.trend} />
              </ChartPanel>
              <ChartPanel
                title="Raw Discipline Dağılımı"
                subtitle={data.appliedDiscipline ? `Uygulanan Discipline: ${data.appliedDiscipline}` : 'Historical kaynak değerleri'}
                reliability={data.metadata.reliability}
              >
                <HorizontalBarChart data={data.byDiscipline.map((item) => ({ label: item.category, count: item.count }))} />
              </ChartPanel>
            </div>
          </>
        )
      ) : null}

      <InfoNote>
        Kaynak <span className="code-chip">analytics.HistoricalWorkOrders</span> ayrı bir dataset'tir.
        GREEN etiketi yalnız historical record count ve trend sözleşmesinin güvenilirliğini belirtir; asset güvenilirliği değildir.
      </InfoNote>
    </section>
  )
}
