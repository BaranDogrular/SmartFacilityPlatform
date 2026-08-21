import { useState, type FormEvent } from 'react'
import type { WorkOrderAnalyticsQuery } from '../api/analyticsTypes'
import { ChartPanel, HorizontalBarChart, TrendLineChart } from '../components/AnalyticsCharts'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { HistoricalMaintenanceActivity } from '../components/HistoricalMaintenanceActivity'
import { useWorkOrderOverview, useWorkOrderTrend } from '../hooks/useAnalytics'

const emptyFilters: WorkOrderAnalyticsQuery = {}

export function WorkOrdersPage() {
  const [draft, setDraft] = useState<WorkOrderAnalyticsQuery>(emptyFilters)
  const [filters, setFilters] = useState<WorkOrderAnalyticsQuery>(emptyFilters)
  const [filterError, setFilterError] = useState('')
  const optionsQuery = useWorkOrderOverview()
  const overview = useWorkOrderOverview(filters)
  const trend = useWorkOrderTrend({ ...filters, grain: 'Month' })

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
    return <LoadingState label="İş emri analitiği yükleniyor" />
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
        eyebrow="Operasyonel iş yükü"
        title="Güncel iş emri görünümü"
        description="Disiplin, iş tipi, durum ve kategori dağılımları ile aylık hareket."
        actions={<DataTimestamp value={data.metadata.dataAsOf} />}
      />

      <form className="filter-panel" onSubmit={applyFilters} aria-label="İş emri filtreleri">
        <div className="filter-grid">
          <label>
            <span>Başlangıç tarihi</span>
            <input
              className="form-control"
              type="date"
              value={draft.dateFrom ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateFrom: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Bitiş tarihi</span>
            <input
              className="form-control"
              type="date"
              value={draft.dateTo ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, dateTo: event.target.value || undefined }))}
            />
          </label>
          <label>
            <span>Disiplin</span>
            <select
              className="form-select"
              value={draft.discipline ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, discipline: event.target.value || undefined }))}
            >
              <option value="">Tümü</option>
              {optionsQuery.data.byDiscipline.map((item) => <option key={item.category} value={item.category}>{item.category}</option>)}
            </select>
          </label>
          <label>
            <span>Durum</span>
            <select
              className="form-select"
              value={draft.status ?? ''}
              onChange={(event) => setDraft((current) => ({ ...current, status: event.target.value || undefined }))}
            >
              <option value="">Tümü</option>
              {optionsQuery.data.byStatus.map((item) => <option key={item.category} value={item.category}>{item.category}</option>)}
            </select>
          </label>
        </div>
        {filterError ? <p className="filter-error" role="alert">{filterError}</p> : null}
        <div className="filter-actions">
          <button className="btn btn-primary" type="submit">Filtreleri uygula</button>
          <button className="btn btn-outline-secondary" type="button" onClick={resetFilters}>Temizle</button>
        </div>
      </form>

      {data.totalWorkOrders === 0 ? (
        <EmptyState />
      ) : (
        <>
          <section className="kpi-grid kpi-grid--one" aria-label="İş emri göstergeleri">
            <KpiCard label="Toplam Güncel İş Emri" value={data.totalWorkOrders} reliability={data.metadata.reliability} tone="teal" />
          </section>

          <ChartPanel title="Aylık İş Emri Trendi" subtitle="ReportedDateTime alanına göre" reliability={trend.data.metadata.reliability}>
            <TrendLineChart points={trend.data.points} />
          </ChartPanel>

          <div className="dashboard-grid dashboard-grid--two">
            <ChartPanel title="Disiplin Dağılımı" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byDiscipline.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="İş Tipi Dağılımı" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byWorkType.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Durum Dağılımı" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byStatus.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Arıza / İş Kategorisi" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byFailureType.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Bina Dağılımı" reliability={data.byBuildingReliability}>
              <HorizontalBarChart data={data.byBuilding.map((item) => ({ label: item.name, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Lokasyon Dağılımı" reliability={data.byLocationReliability}>
              <HorizontalBarChart data={data.byLocation.map((item) => ({ label: item.name, count: item.count }))} />
            </ChartPanel>
          </div>
        </>
      )}

      <InfoNote>Bu görünüm yalnız güncel WorkOrder veri kaynağını içerir; geçmiş analitik kayıtlar bu toplamlara dahil değildir.</InfoNote>

      <HistoricalMaintenanceActivity />
    </div>
  )
}
