import { useState, type FormEvent } from 'react'
import type { WorkOrderAnalyticsQuery } from '../api/analyticsTypes'
import { ChartPanel, HorizontalBarChart, TrendLineChart } from '../components/AnalyticsCharts'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { WorkOrderActivity } from '../components/WorkOrderActivity'
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
        eyebrow="Canonical iş emri verisi"
        title="İş Emirleri"
        description="Toplam kaynak dataset'indeki açık, kapalı ve diğer source durumları ile workflow dağılımları."
        actions={<DataTimestamp value={data.metadata.dataAsOf} />}
      />

      <section className="dataset-section" aria-label="Canonical iş emri veri seti">
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
            <span>Workflow statüsü</span>
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
          <button className="btn btn-primary" type="submit">Uygula</button>
          <button className="btn btn-outline-secondary" type="button" onClick={resetFilters}>Temizle</button>
        </div>
      </form>

      {data.totalWorkOrders === 0 ? (
        <EmptyState />
      ) : (
        <>
          <section className="kpi-grid" aria-label="İş emri göstergeleri">
            <KpiCard label="Toplam İş Emri" value={data.totalWorkOrders} reliability={data.metadata.reliability} tone="teal" />
            <KpiCard label="Açık İş Emri" value={data.openWorkOrders} note="Source durum kodu: A" reliability={data.metadata.reliability} tone="amber" />
            <KpiCard label="Kapalı İş Emri" value={data.closedWorkOrders} note="Source durum kodu: K" reliability={data.metadata.reliability} tone="navy" />
            <KpiCard label="Son 30 Gün Aktivitesi" value={data.last30DaysWorkOrders} note="Durumdan bağımsız kayıt aktivitesi" reliability={data.metadata.reliability} tone="slate" />
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
            <ChartPanel title="İş Emri Source Durumu" subtitle="A: Açık, K: Kapalı, diğer kodlar ayrı" reliability={data.metadata.reliability}>
              <HorizontalBarChart data={data.byRawStatusCode.map((item) => ({ label: item.category, count: item.count }))} />
            </ChartPanel>
            <ChartPanel title="Workflow Statü Dağılımı" subtitle="Source açık/kapalı durumundan farklıdır" reliability={data.metadata.reliability}>
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

      <InfoNote>
        Açık ve kapalı sayıları source <span className="code-chip">RawStatusCode</span> alanından hesaplanır.
        Workflow statüsü bu sayımları belirlemez. Diğer durumlar: {data.otherWorkOrders}.
      </InfoNote>
      </section>

      <WorkOrderActivity />
    </div>
  )
}
