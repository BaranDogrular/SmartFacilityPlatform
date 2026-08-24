import type { ScadaClearanceIntervalQuery } from '../api/analyticsTypes'
import { useScadaClearanceInterval } from '../hooks/useAnalytics'
import { formatCount, formatDecimal, formatPercent } from '../utils/format'
import { EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, ReliabilityBadge } from './DashboardUi'

export function ScadaClearanceInterval({ query }: { query: ScadaClearanceIntervalQuery }) {
  const clearanceQuery = useScadaClearanceInterval(query)

  if (clearanceQuery.isPending) {
    return <LoadingState label="SCADA clearance interval yükleniyor" />
  }

  if (clearanceQuery.error) {
    return <ErrorState error={clearanceQuery.error} onRetry={() => void clearanceQuery.refetch()} />
  }

  if (!clearanceQuery.data) {
    return <EmptyState />
  }

  const data = clearanceQuery.data

  return (
    <section className="analytics-section analytics-section--scada" aria-labelledby="scada-clearance-title">
      <header className="analytics-section__header">
        <div>
          <p className="page-eyebrow">Quality-eligible occurrence metriği</p>
          <h2 id="scada-clearance-title">SCADA Clearance Interval</h2>
          <p>ReceivedAt ile ClearedAt arasındaki gözlenen clearance aralığı.</p>
        </div>
        <ReliabilityBadge reliability={data.metadata.reliability} />
      </header>

      {data.totalMatchedOccurrences === 0 ? (
        <EmptyState message="Seçilen filtrelerle clearance aralığı hesaplanabilecek occurrence bulunamadı." />
      ) : (
        <>
          <section className="kpi-grid kpi-grid--quality" aria-label="SCADA clearance interval göstergeleri">
            <KpiCard
              label="Median Clearance"
              value={data.medianMinutes === null ? 'Hesaplanamadı' : `${formatDecimal(data.medianMinutes)} dk`}
              note="Ana gösterge"
              reliability={data.metadata.reliability}
              tone="teal"
            />
            <KpiCard
              label="P90 Clearance"
              value={data.p90Minutes === null ? 'Hesaplanamadı' : `${formatDecimal(data.p90Minutes)} dk`}
              note="İkincil dağılım göstergesi"
              reliability={data.metadata.reliability}
              tone="slate"
            />
            <KpiCard
              label="Eşleşen Occurrence"
              value={data.totalMatchedOccurrences}
              reliability={data.metadata.reliability}
            />
            <KpiCard
              label="Eligibility"
              value={data.eligibilityPercent === null ? '—' : formatPercent(data.eligibilityPercent)}
              reliability={data.metadata.reliability}
              tone="amber"
            />
          </section>

          <div className="quality-summary" aria-label="SCADA clearance veri kalitesi özeti">
            <span>Eligible occurrence <strong>{formatCount(data.eligibleOccurrences)}</strong></span>
            <span>Kalite nedeniyle dışlanan <strong>{formatCount(data.excludedOccurrences)}</strong></span>
            <span>Toplam eşleşen <strong>{formatCount(data.totalMatchedOccurrences)}</strong></span>
          </div>
        </>
      )}

      <InfoNote>
        Median ve P90 yalnız timestamp kalite kurallarını geçen source occurrence alt kümesinde hesaplanır;
        fiziksel müdahale süresini veya benzersiz fiziksel alarm sayısını temsil etmez.
      </InfoNote>
    </section>
  )
}
