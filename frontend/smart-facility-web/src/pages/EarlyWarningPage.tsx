import { useState, type FormEvent } from 'react'
import type { EarlyWarningLevel, EarlyWarningQuery } from '../api/analyticsTypes'
import { EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { useEarlyWarning } from '../hooks/useAnalytics'
import { formatCount, formatDecimal, formatPercent } from '../utils/format'

const defaultQuery: EarlyWarningQuery = { top: 10 }

const levelLabels: Record<EarlyWarningLevel, string> = {
  HIGH: 'YÜKSEK SAPMA',
  MEDIUM: 'İZLE',
  NORMAL: 'NORMAL',
}

export function EarlyWarningPage() {
  const [draft, setDraft] = useState<EarlyWarningQuery>(defaultQuery)
  const [query, setQuery] = useState<EarlyWarningQuery>(defaultQuery)
  const warning = useEarlyWarning(query)

  const applyFilters = (event: FormEvent) => {
    event.preventDefault()
    setQuery({ ...draft })
  }

  const clearFilters = () => {
    setDraft(defaultQuery)
    setQuery(defaultQuery)
  }

  if (warning.isPending) {
    return <LoadingState label="Erken uyarı aktivite sapmaları hesaplanıyor" />
  }

  if (warning.error) {
    return <ErrorState error={warning.error} onRetry={() => void warning.refetch()} />
  }

  if (!warning.data) {
    return <EmptyState />
  }

  const { metadata, items } = warning.data
  const visibleLevelCounts = {
    high: items.filter((item) => item.warningLevel === 'HIGH').length,
    medium: items.filter((item) => item.warningLevel === 'MEDIUM').length,
    normal: items.filter((item) => item.warningLevel === 'NORMAL').length,
    insufficient: items.filter((item) => item.baselineStatus === 'INSUFFICIENT_BASELINE').length,
  }

  return (
    <div className="page-stack page-stack--early-warning">
      <PageHeader
        eyebrow="Açıklanabilir davranış sapması"
        title="Erken Uyarı"
        description="Varlıkların yakın dönem iş emri aktivitesinde kendi geçmiş davranışlarına göre oluşan anlamlı sapmaları gösterir."
        actions={metadata.asOf ? <span className="data-timestamp">Analiz tarihi: {formatDate(metadata.asOf)}</span> : null}
      />

      <InfoNote>
        Bu gösterge arıza olasılığı veya varlık sağlık skoru değildir. Bir asset&apos;in kendi WorkOrder aktivite
        geçmişinden sapmasını gösterir; otomatik bakım kararı üretmez.
      </InfoNote>

      <form className="filter-panel" onSubmit={applyFilters} aria-label="Erken uyarı filtreleri">
        <header className="filter-panel__header">
          <div><span>Analiz kapsamı</span><strong>Sapma filtresi</strong></div>
          <small>Tarih ve sonuç sayısını seçin</small>
        </header>
        <div className="filter-grid filter-grid--early-warning">
          <label>
            <span>Analiz tarihi</span>
            <input
              className="form-control"
              name="earlyWarningAsOf"
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
              name="earlyWarningTop"
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

      <section className="kpi-grid kpi-grid--five" aria-label="Erken uyarı kapsamı">
        <KpiCard label="Değerlendirilen Varlık" value={metadata.totalAssetsConsidered} tone="teal" />
        <KpiCard label="Baseline Yeterli" value={metadata.eligibleAssets} />
        <KpiCard label="Baseline Yetersiz" value={metadata.insufficientBaselineAssets} tone="amber" />
        <KpiCard label="Asset Linkage Coverage" value={formatPercent(metadata.coveragePercent)} tone="slate" />
        <KpiCard label="Analiz Tarihi" value={metadata.asOf ? formatDate(metadata.asOf) : '—'} tone="slate" />
      </section>

      <section className="signal-summary signal-summary--warning" aria-label="Görünen erken uyarı seviyeleri">
        <div className="signal-summary__intro">
          <span>Görünen sonuçlar</span>
          <strong>Uyarı dağılımı</strong>
          <small>Seçili Top-{metadata.appliedTop} kapsamı</small>
        </div>
        <div className="signal-summary__metric signal-summary__metric--high">
          <span>HIGH</span><strong>{formatCount(visibleLevelCounts.high)}</strong><small>Yüksek sapma</small>
        </div>
        <div className="signal-summary__metric signal-summary__metric--medium">
          <span>MEDIUM</span><strong>{formatCount(visibleLevelCounts.medium)}</strong><small>İzle</small>
        </div>
        <div className="signal-summary__metric signal-summary__metric--normal">
          <span>NORMAL</span><strong>{formatCount(visibleLevelCounts.normal)}</strong><small>Normal davranış</small>
        </div>
        <div className="signal-summary__metric signal-summary__metric--insufficient">
          <span>YETERSİZ</span><strong>{formatCount(visibleLevelCounts.insufficient)}</strong><small>Baseline yok</small>
        </div>
      </section>

      {items.length === 0 ? (
        <EmptyState message="Seçilen analiz tarihinde değerlendirilebilecek bağlı varlık bulunamadı." />
      ) : (
        <section className="table-panel early-warning-panel" aria-labelledby="early-warning-table-title">
          <div className="early-warning-panel__header">
            <div>
              <h2 id="early-warning-table-title">Aktivite Sapması Sıralaması</h2>
              <p>Score, asset&apos;in kendi 12 aylık baseline davranışına göre hesaplanır.</p>
            </div>
            <span className="code-chip">{metadata.scoringVersion}</span>
          </div>
          <div className="early-warning-table-wrap">
            <table className="analytics-table early-warning-table">
              <thead>
                <tr>
                  <th>Sıra</th>
                  <th>Varlık</th>
                  <th>Uyarı</th>
                  <th className="text-end">Puan</th>
                  <th className="text-end">Son 7</th>
                  <th className="text-end">Önceki 7</th>
                  <th className="text-end">Son 30</th>
                  <th className="text-end">Önceki 30</th>
                  <th className="text-end">Baseline</th>
                  <th className="text-end">Açık</th>
                  <th>Neden</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item, index) => (
                  <tr key={item.assetId}>
                    <td className="early-warning-rank">{index + 1}</td>
                    <td className="early-warning-asset">
                      <strong>{item.assetCode}</strong>
                      <span>{item.assetName}</span>
                    </td>
                    <td>
                      {item.baselineStatus === 'INSUFFICIENT_BASELINE' || !item.warningLevel ? (
                        <span className="warning-badge warning-badge--insufficient">BASELINE YETERSİZ</span>
                      ) : (
                        <span className={`warning-badge warning-badge--${item.warningLevel.toLowerCase()}`}>
                          {levelLabels[item.warningLevel]}
                        </span>
                      )}
                    </td>
                    <td className="text-end early-warning-score">
                      {item.warningScore === null ? '—' : formatDecimal(item.warningScore)}
                    </td>
                    <td className="text-end">{formatCount(item.last7Count)}</td>
                    <td className="text-end">{formatCount(item.previous7Count)}</td>
                    <td className="text-end">{formatCount(item.last30Count)}</td>
                    <td className="text-end">{formatCount(item.previous30Count)}</td>
                    <td className="text-end">
                      {item.baselineMedian === null
                        ? `${item.baselineActiveMonths}/12 ay`
                        : `${formatDecimal(item.baselineMedian)} medyan`}
                    </td>
                    <td className="text-end">{formatCount(item.openCount)}</td>
                    <td className="early-warning-reasons">
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
        İnceleme Önceliği “nereye önce bakmalıyım?” sorusuna; Erken Uyarı ise “hangi asset kendi normal
        davranışından sapıyor?” sorusuna yanıt verir. İki score bir risk puanında birleştirilmez.
      </InfoNote>

      <InfoNote>
        Baseline: {metadata.baselineWindow
          ? `${formatDate(metadata.baselineWindow.from)}–${formatDate(metadata.baselineWindow.through)}`
          : '—'}. En az {metadata.baselineWindow?.minimumActiveMonths ?? 6} aktif ay gerekir.{' '}
        {formatCount(metadata.excludedUnlinkedWorkOrders)} unlinked kayıt hesaplamaya dahil edilmemiştir.
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
