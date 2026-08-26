import { useState, type FormEvent } from 'react'
import type { InspectionPriorityLevel, InspectionPriorityQuery } from '../api/analyticsTypes'
import { EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { useInspectionPriority } from '../hooks/useAnalytics'
import { formatCount, formatDecimal, formatPercent } from '../utils/format'

const defaultQuery: InspectionPriorityQuery = { top: 10 }

const levelLabels: Record<InspectionPriorityLevel, string> = {
  HIGH: 'HIGH · Öncelikli inceleme',
  MEDIUM: 'MEDIUM · Yakın izleme',
  LOW: 'LOW · Düşük öncelik',
}

export function InspectionPriorityPage() {
  const [draft, setDraft] = useState<InspectionPriorityQuery>(defaultQuery)
  const [query, setQuery] = useState<InspectionPriorityQuery>(defaultQuery)
  const priority = useInspectionPriority(query)

  const applyFilters = (event: FormEvent) => {
    event.preventDefault()
    setQuery({ ...draft })
  }

  const clearFilters = () => {
    setDraft(defaultQuery)
    setQuery(defaultQuery)
  }

  if (priority.isPending) {
    return <LoadingState label="İnceleme önceliği hesaplanıyor" />
  }

  if (priority.error) {
    return <ErrorState error={priority.error} onRetry={() => void priority.refetch()} />
  }

  if (!priority.data) {
    return <EmptyState />
  }

  const { metadata, items } = priority.data

  return (
    <div className="page-stack page-stack--inspection-priority">
      <PageHeader
        eyebrow="Açıklanabilir bakım karar desteği"
        title="İnceleme Önceliği"
        description="Yakın dönem iş emri aktivitesine göre hangi varlıkların önce incelenmesinin daha anlamlı olduğunu gösterir."
        actions={metadata.asOf ? <span className="data-timestamp">Analiz tarihi: {formatDate(metadata.asOf)}</span> : null}
      />

      <InfoNote>
        Bu gösterge arıza olasılığı veya varlık sağlık skoru değildir. Otomatik bakım kararı vermez;
        yalnız WorkOrder aktivitesine dayalı inceleme sıralaması sunar.
      </InfoNote>

      <form className="filter-panel" onSubmit={applyFilters} aria-label="İnceleme önceliği filtreleri">
        <div className="filter-grid filter-grid--inspection-priority">
          <label>
            <span>Analiz tarihi</span>
            <input
              className="form-control"
              name="inspectionAsOf"
              type="date"
              value={draft.asOf ?? ''}
              onChange={(event) => setDraft((current) => ({
                ...current,
                asOf: event.target.value || undefined,
              }))}
            />
          </label>
          <label>
            <span>Gösterilecek varlık</span>
            <select
              className="form-select"
              name="inspectionTop"
              value={draft.top ?? 10}
              onChange={(event) => setDraft((current) => ({
                ...current,
                top: Number(event.target.value),
              }))}
            >
              {[10, 25, 50, 100].map((value) => <option key={value} value={value}>Top {value}</option>)}
            </select>
          </label>
        </div>
        <div className="filter-actions">
          <button className="btn btn-primary" type="submit">Uygula</button>
          <button className="btn btn-outline-secondary" type="button" onClick={clearFilters}>Temizle</button>
        </div>
      </form>

      <section className="kpi-grid kpi-grid--five" aria-label="İnceleme önceliği kapsamı">
        <KpiCard label="Değerlendirilen Varlık" value={metadata.totalAssetsEvaluated} tone="teal" />
        <KpiCard label="Eligible İş Emri" value={metadata.eligibleWorkOrders} />
        <KpiCard label="Dışlanan Unlinked" value={metadata.excludedUnlinkedWorkOrders} tone="amber" />
        <KpiCard label="Asset Linkage Coverage" value={formatPercent(metadata.coveragePercent)} tone="slate" />
        <KpiCard label="Analiz Tarihi" value={metadata.asOf ? formatDate(metadata.asOf) : '—'} tone="slate" />
      </section>

      {items.length === 0 ? (
        <EmptyState message="Seçilen analiz tarihinde son 90 gün aktivitesi veya açık iş yükü bulunan bağlı varlık yok." />
      ) : (
        <section className="table-panel inspection-priority-panel" aria-labelledby="inspection-priority-table-title">
          <div className="inspection-priority-panel__header">
            <div>
              <h2 id="inspection-priority-table-title">Öncelik Sıralaması</h2>
              <p>Puan, açık iş yükü, son 30 gün aktivitesi ve varlık koduyla deterministik sıralanır.</p>
            </div>
            <span className="code-chip">{metadata.scoringVersion}</span>
          </div>
          <div className="inspection-table-wrap">
            <table className="analytics-table inspection-priority-table">
              <thead>
                <tr>
                  <th>Sıra</th>
                  <th>Varlık</th>
                  <th>Öncelik</th>
                  <th className="text-end">Puan</th>
                  <th className="text-end">Son 7 Gün</th>
                  <th className="text-end">Son 30 Gün</th>
                  <th className="text-end">Önceki 30 Gün</th>
                  <th className="text-end">Son 90 Gün</th>
                  <th className="text-end">Açık</th>
                  <th>Neden</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item, index) => (
                  <tr key={item.assetId}>
                    <td className="inspection-rank">{index + 1}</td>
                    <td className="inspection-asset">
                      <strong>{item.assetCode}</strong>
                      <span>{item.assetName}</span>
                    </td>
                    <td>
                      <span className={`priority-badge priority-badge--${item.priorityLevel.toLowerCase()}`}>
                        {levelLabels[item.priorityLevel]}
                      </span>
                    </td>
                    <td className="text-end inspection-score">{formatDecimal(item.priorityScore)}</td>
                    <td className="text-end">{formatCount(item.last7Count)}</td>
                    <td className="text-end">{formatCount(item.last30Count)}</td>
                    <td className="text-end">{formatCount(item.previous30Count)}</td>
                    <td className="text-end">{formatCount(item.last90Count)}</td>
                    <td className="text-end">{formatCount(item.openCount)}</td>
                    <td className="inspection-reasons">
                      <ul>
                        {item.reasons.map((reason) => <li key={reason}>{reason}</li>)}
                      </ul>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <InfoNote>
        Priority hesabı yalnız AssetId bağlı canonical WorkOrder kayıtlarını kullanır. {formatCount(metadata.excludedUnlinkedWorkOrders)}
        {' '}unlinked kayıt puana dahil edilmemiştir. SCADA ve legacy HistoricalWorkOrders kullanılmaz.
      </InfoNote>
    </div>
  )
}

function formatDate(value: string): string {
  const parsed = new Date(`${value.slice(0, 10)}T00:00:00`)
  return Number.isNaN(parsed.getTime())
    ? value
    : new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' }).format(parsed)
}
