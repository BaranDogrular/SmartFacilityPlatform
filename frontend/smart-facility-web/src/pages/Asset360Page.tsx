import { Link, useParams } from 'react-router-dom'
import { AnalyticsApiError } from '../api/analyticsClient'
import type { EarlyWarningLevel, InspectionPriorityLevel } from '../api/analyticsTypes'
import { ChartPanel, TrendLineChart } from '../components/AnalyticsCharts'
import { AssetActivityTimeline } from '../components/AssetActivityTimeline'
import {
  EmptyState,
  ErrorState,
  InfoNote,
  KpiCard,
  LoadingState,
  PageHeader,
  ReliabilityBadge,
} from '../components/DashboardUi'
import { useAsset360Summary, useWorkOrderTrend } from '../hooks/useAnalytics'
import { formatCount, formatDecimal, formatPercent } from '../utils/format'

const priorityLabels: Record<InspectionPriorityLevel, string> = {
  HIGH: 'YÜKSEK · Öncelikli inceleme',
  MEDIUM: 'ORTA · Yakın izleme',
  LOW: 'DÜŞÜK · Düşük öncelik',
}

const warningLabels: Record<EarlyWarningLevel, string> = {
  HIGH: 'YÜKSEK · Yüksek sapma',
  MEDIUM: 'ORTA · İzle',
  NORMAL: 'NORMAL · Beklenen aralık',
}

export function Asset360Page() {
  const { assetId: routeAssetId } = useParams()
  const assetId = Number(routeAssetId)
  const hasValidAssetId = Number.isSafeInteger(assetId) && assetId > 0
  const summary = useAsset360Summary(assetId, hasValidAssetId)
  const trend = useWorkOrderTrend(
    { assetId, grain: 'Month' },
    hasValidAssetId,
  )

  if (!hasValidAssetId) {
    return <AssetNotFound message="Geçerli bir varlık kimliği belirtilmedi." />
  }

  if (summary.isPending || trend.isPending) {
    return <LoadingState label="Asset 360 özeti hazırlanıyor" />
  }

  if (summary.error instanceof AnalyticsApiError && summary.error.status === 404) {
    return <AssetNotFound message="Bu kimlikle eşleşen doğrulanmış varlık bulunamadı." />
  }

  if (summary.error) {
    return <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />
  }

  if (trend.error) {
    return <ErrorState error={trend.error} onRetry={() => void trend.refetch()} />
  }

  if (!summary.data || !trend.data) {
    return <EmptyState message="Asset 360 özeti alınamadı." />
  }

  const data = summary.data
  const identity = data.identity
  const maintenance = data.maintenance
  const priority = data.inspectionPriority
  const warning = data.earlyWarning
  const contextPath = [identity.buildingName, identity.locationName, identity.assetGroupName]
    .filter((value): value is string => Boolean(value))
    .join(' → ')

  return (
    <div className="page-stack page-stack--asset360">
      <PageHeader
        eyebrow="Varlık 360"
        title={identity.assetName}
        description={`${identity.assetCode}${contextPath ? ` · ${contextPath}` : ''}`}
        actions={(
          <Link className="btn btn-outline-secondary" to="/assets">
            ← Varlıklara dön
          </Link>
        )}
      />

      <section className="asset360-identity" aria-labelledby="asset360-identity-title">
        <header>
          <div>
            <span className="code-chip">{identity.assetCode}</span>
            <h2 id="asset360-identity-title">Varlık Kimliği</h2>
          </div>
          {data.asOf ? <span className="data-timestamp">Analiz tarihi: {formatDate(data.asOf)}</span> : null}
        </header>
        <dl className="asset360-identity__grid">
          <IdentityField label="Bina" value={identity.buildingName} />
          <IdentityField label="Lokasyon" value={identity.locationName} />
          <IdentityField label="Varlık grubu" value={identity.assetGroupName} />
          <IdentityField label="Tür" value={identity.assetType} />
          <IdentityField label="Durum" value={identity.status} />
          <IdentityField label="Seri numarası" value={identity.serialNumber} />
          <IdentityField
            label="Varlık kaydındaki son bakım"
            value={identity.lastMaintenanceDate ? formatDate(identity.lastMaintenanceDate) : null}
          />
          <div>
            <dt>Üst varlık</dt>
            <dd>
              {identity.parentAsset ? (
                <Link className="asset-detail-link" to={`/assets/${identity.parentAsset.assetId}`}>
                  {identity.parentAsset.assetCode} · {identity.parentAsset.assetName}
                </Link>
              ) : 'Bilgi bulunmuyor'}
            </dd>
          </div>
        </dl>
      </section>

      <section className="kpi-grid kpi-grid--six" aria-label="Güncel bakım aktivitesi">
        <KpiCard
          label="Toplam İş Emri"
          value={maintenance.totalWorkOrders}
          note="Varlıkla eşleşen güncel iş emri kaydı"
        />
        <KpiCard label="Açık İş Emri" value={maintenance.openWorkOrders} tone="amber" />
        <KpiCard label="Son 7 Gün" value={maintenance.last7Count} tone="teal" />
        <KpiCard label="Son 30 Gün" value={maintenance.last30Count} />
        <KpiCard label="Son 90 Gün" value={maintenance.last90Count} tone="slate" />
        <KpiCard
          label="Son kayıtlı iş emri"
          value={maintenance.lastWorkOrderDate ? formatDate(maintenance.lastWorkOrderDate) : 'Bilgi bulunmuyor'}
          tone="slate"
        />
      </section>

      <p className="asset360-decision-note">
        İnceleme Önceliği bakım iş yüküne göre hangi varlıklara önce bakılması gerektiğini; Erken Uyarı ise
        varlığın kendi tarihsel düzeninden sapıp sapmadığını gösterir. Bu nedenle iki sonuç farklı seviyelerde olabilir.
      </p>

      <section className="asset360-decision-grid" aria-label="Açıklanabilir karar desteği">
        <article className="asset360-decision-card asset360-decision-card--priority">
          <header>
            <div>
              <span>Bakım aktivitesi göstergesi</span>
              <h2>İnceleme Önceliği</h2>
            </div>
            <span className={`priority-badge priority-badge--${priority.level.toLowerCase()}`}>
              {priorityLabels[priority.level]}
            </span>
          </header>
          <div className="asset360-score">
            <strong>{formatDecimal(priority.score)}</strong>
            <span>100 üzerinden öncelik puanı</span>
            <small>{priority.scoringVersion}</small>
          </div>
          <SignalGrid signals={[
            ['Son 7 gün', priority.last7Count],
            ['Son 30 gün', priority.last30Count],
            ['Önceki 30 gün', priority.previous30Count],
            ['Son 90 gün', priority.last90Count],
            ['Açık iş yükü', priority.openCount],
            ['Aktivite değişimi', priority.activityChange],
          ]} />
          <ReasonList reasons={priority.reasons} emptyMessage="İnceleme önceliğini yükselten aktivite sinyali yok." />
          <InfoNote>
            Bu, bakım aktivitesine dayalı bir inceleme önceliği göstergesidir; arıza olasılığı değildir.
          </InfoNote>
        </article>

        <article className="asset360-decision-card asset360-decision-card--warning">
          <header>
            <div>
              <span>Kişisel tarihsel baseline</span>
              <h2>Erken Uyarı</h2>
            </div>
            {warning.baselineStatus === 'INSUFFICIENT_BASELINE' || !warning.level ? (
              <span className="warning-badge warning-badge--insufficient">YETERSİZ GEÇMİŞ VERİ</span>
            ) : (
              <span className={`warning-badge warning-badge--${warning.level.toLowerCase()}`}>
                {warningLabels[warning.level]}
              </span>
            )}
          </header>
          <div className="asset360-score">
            <strong>{warning.score === null ? '—' : formatDecimal(warning.score)}</strong>
            <span>{warning.score === null ? 'Yetersiz geçmiş veri' : '100 üzerinden sapma puanı'}</span>
            <small>{warning.scoringVersion}</small>
          </div>
          <SignalGrid signals={[
            ['Son 7 / Önceki 7', `${formatCount(warning.last7Count)} / ${formatCount(warning.previous7Count)}`],
            ['Son 30 / Önceki 30', `${formatCount(warning.last30Count)} / ${formatCount(warning.previous30Count)}`],
            ['Son 90 / Önceki 90', `${formatCount(warning.last90Count)} / ${formatCount(warning.previous90Count)}`],
            ['Baseline aktif ay', `${formatCount(warning.baselineActiveMonths)}/12`],
            ['Median / MAD', warning.baselineMedian === null ? '—' : `${formatDecimal(warning.baselineMedian)} / ${formatDecimal(warning.baselineMad ?? 0)}`],
            ['Açık iş yükü', warning.openCount],
          ]} />
          {warning.components ? (
            <div className="asset360-components" aria-label="Erken uyarı doğrulanmış puan bileşenleri">
              <span>Puan katkıları</span>
              <small>İvme {formatDecimal(warning.components.acceleration)}</small>
              <small>Kısa dönem {formatDecimal(warning.components.shortTermSpike)}</small>
              <small>Tarihsel sapma {formatDecimal(warning.components.historicalDeviation)}</small>
              <small>Tekrarlama {formatDecimal(warning.components.recurrenceBurst)}</small>
              <small>Açık yük {formatDecimal(warning.components.openEmergence)}</small>
            </div>
          ) : null}
          <ReasonList reasons={warning.reasons} emptyMessage="Erken uyarı nedeni bulunmuyor." />
        </article>
      </section>

      <ChartPanel
        title="Aylık İş Emri Trendi"
        subtitle="Yalnızca bu varlıkla doğrulanmış şekilde eşleşen güncel iş emirleri"
        reliability={trend.data.metadata.reliability}
        localizedReliability
      >
        <TrendLineChart points={trend.data.points} reduceTickDensity />
        <InfoNote>Trend, aktivite değişimini gösterir; arıza olasılığı veya nedensellik göstermez.</InfoNote>
      </ChartPanel>

      <AssetActivityTimeline key={assetId} assetId={assetId} />

      <section className="asset360-scope" aria-labelledby="asset360-scope-title">
        <header>
          <div>
            <span>Veri kapsamı</span>
            <h2 id="asset360-scope-title">Bağlantı ve Güvenilirlik Notu</h2>
          </div>
          <ReliabilityBadge reliability={data.scope.reliability} localized />
        </header>
        <div className="asset360-scope__metrics">
          <span>Varlıkla eşleşen kayıt <strong>{formatCount(data.scope.linkedCanonicalWorkOrders)}</strong></span>
          <span>Varlıkla eşleşmeyen kayıt <strong>{formatCount(data.scope.excludedUnlinkedCanonicalWorkOrders)}</strong></span>
          <span>Eşleşme oranı <strong>{formatPercent(data.scope.linkageCoveragePercent)}</strong></span>
          <span>
            Kaynak
            <strong title={data.scope.sourceDataset}>Doğrulanmış varlık ve güncel iş emri verileri</strong>
          </span>
        </div>
        <ul>
          <li>Varlık bazlı sonuçlar yalnızca doğrulanmış şekilde eşleşen güncel iş emirlerini kullanır.</li>
          <li>Varlıkla eşleşmeyen iş emirleri varlık bazlı hesaplamalara dahil edilmez.</li>
          <li>Eski tarihsel veri seti güncel analiz sonuçlarına dahil edilmez.</li>
          <li>Güvenilir asset bağlantısı olmadığı için SCADA ve outage verileri gösterilmez.</li>
        </ul>
      </section>
    </div>
  )
}

function AssetNotFound({ message }: { message: string }) {
  return (
    <section className="asset360-not-found" role="status">
      <p className="page-eyebrow">Asset 360</p>
      <h1>Varlık bulunamadı</h1>
      <p>{message}</p>
      <Link className="btn btn-outline-secondary" to="/assets">Varlıklara dön</Link>
    </section>
  )
}

function IdentityField({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value || 'Bilgi bulunmuyor'}</dd>
    </div>
  )
}

function SignalGrid({ signals }: { signals: Array<[string, number | string]> }) {
  return (
    <dl className="asset360-signal-grid">
      {signals.map(([label, value]) => (
        <div key={label}>
          <dt>{label}</dt>
          <dd>{typeof value === 'number' ? formatCount(value) : value}</dd>
        </div>
      ))}
    </dl>
  )
}

function ReasonList({ reasons, emptyMessage }: { reasons: string[]; emptyMessage: string }) {
  return (
    <div className="asset360-reasons">
      <h3>Bu sonuç neden oluştu?</h3>
      {reasons.length === 0 ? <p>{emptyMessage}</p> : (
        <ul>{reasons.map((reason) => <li key={reason}>{reason}</li>)}</ul>
      )}
    </div>
  )
}

function formatDate(value: string): string {
  const parsed = new Date(value.length === 10 ? `${value}T00:00:00` : value)
  return Number.isNaN(parsed.getTime())
    ? value
    : new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' }).format(parsed)
}
