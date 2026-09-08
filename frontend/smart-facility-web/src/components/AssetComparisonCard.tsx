import { Link } from 'react-router-dom'
import { AnalyticsApiError } from '../api/analyticsClient'
import type {
  Asset360SummaryResponse,
  EarlyWarningLevel,
  InspectionPriorityLevel,
} from '../api/analyticsTypes'
import { formatCount, formatDecimal, getErrorMessage } from '../utils/format'

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

interface AssetComparisonCardProps {
  assetId: number
  data?: Asset360SummaryResponse
  error: unknown
  isPending: boolean
  isFetching: boolean
  onRetry: () => void
  onRemove: () => void
}

export function AssetComparisonCard({
  assetId,
  data,
  error,
  isPending,
  isFetching,
  onRetry,
  onRemove,
}: AssetComparisonCardProps) {
  if (isPending) {
    return (
      <article className="asset-comparison-card asset-comparison-card--state" aria-busy="true" role="status">
        <h2>Varlık {assetId}</h2>
        <p>Varlık özeti yükleniyor…</p>
        <button className="btn btn-sm btn-outline-secondary" type="button" onClick={onRemove}>
          Varlık {assetId} seçimini kaldır
        </button>
      </article>
    )
  }

  if (error || !data) {
    const notFound = error instanceof AnalyticsApiError && error.status === 404
    return (
      <article className="asset-comparison-card asset-comparison-card--state asset-comparison-card--error" role="alert">
        <h2>Varlık {assetId}</h2>
        <strong>{notFound ? 'Varlık bulunamadı' : 'Varlık özeti alınamadı'}</strong>
        <p>{notFound ? 'Bu kimlikle eşleşen varlık bulunamadı.' : getErrorMessage(error)}</p>
        <div className="asset-comparison-card__state-actions">
          <button className="btn btn-sm btn-outline-primary" type="button" onClick={onRetry}>
            Tekrar dene
          </button>
          <button className="btn btn-sm btn-outline-secondary" type="button" onClick={onRemove}>
            Varlık {assetId} seçimini kaldır
          </button>
        </div>
      </article>
    )
  }

  const { identity, maintenance, inspectionPriority, earlyWarning } = data
  const insufficientBaseline = earlyWarning.baselineStatus === 'INSUFFICIENT_BASELINE'

  return (
    <article className="asset-comparison-card" aria-busy={isFetching ? 'true' : undefined}>
      <header className="asset-comparison-card__header">
        <div>
          <span className="code-chip">{identity.assetCode}</span>
          <h2>{identity.assetName}</h2>
        </div>
        <button
          className="asset-comparison-card__remove"
          type="button"
          aria-label={`${identity.assetCode} varlığını karşılaştırmadan kaldır`}
          onClick={onRemove}
        >
          Kaldır
        </button>
      </header>

      <dl className="asset-comparison-card__identity">
        <ComparisonField label="Bina" value={identity.buildingName} />
        <ComparisonField label="Lokasyon" value={identity.locationName} />
        <ComparisonField label="Varlık grubu" value={identity.assetGroupName} />
        <ComparisonField label="Durum" value={identity.status} />
        <ComparisonField label="Tür" value={identity.assetType} />
      </dl>

      <section className="asset-comparison-card__section" aria-label={`${identity.assetCode} bakım aktivitesi`}>
        <h3>Güncel iş emri aktivitesi</h3>
        <dl className="asset-comparison-card__metrics">
          <ComparisonMetric label="Toplam iş emri" value={formatCount(maintenance.totalWorkOrders)} />
          <ComparisonMetric label="Açık iş emri" value={formatCount(maintenance.openWorkOrders)} />
          <ComparisonMetric label="Son 7 gün" value={formatCount(maintenance.last7Count)} />
          <ComparisonMetric label="Son 30 gün" value={formatCount(maintenance.last30Count)} />
          <ComparisonMetric label="Son 90 gün" value={formatCount(maintenance.last90Count)} />
          <ComparisonMetric label="Son kayıtlı iş emri" value={formatOptionalDate(maintenance.lastWorkOrderDate)} />
        </dl>
      </section>

      <section className="asset-comparison-card__section asset-comparison-card__decision">
        <h3>İnceleme Önceliği</h3>
        <div className="asset-comparison-card__score">
          <strong>{formatDecimal(inspectionPriority.score)}</strong>
          <span className={`priority-badge priority-badge--${inspectionPriority.level.toLowerCase()}`}>
            {priorityLabels[inspectionPriority.level]}
          </span>
        </div>
        {inspectionPriority.reasons.length > 0 ? (
          <ul className="asset-comparison-card__reasons">
            {inspectionPriority.reasons.slice(0, 3).map((reason) => <li key={reason}>{reason}</li>)}
          </ul>
        ) : <p className="asset-comparison-card__muted">Önceliği yükselten mevcut aktivite nedeni yok.</p>}
      </section>

      <section className="asset-comparison-card__section asset-comparison-card__decision">
        <h3>Erken Uyarı</h3>
        <div className="asset-comparison-card__score">
          <strong>{earlyWarning.score === null ? 'Bilgi bulunmuyor' : formatDecimal(earlyWarning.score)}</strong>
          {insufficientBaseline || !earlyWarning.level ? (
            <span className="warning-badge warning-badge--insufficient">YETERSİZ GEÇMİŞ VERİ</span>
          ) : (
            <span className={`warning-badge warning-badge--${earlyWarning.level.toLowerCase()}`}>
              {warningLabels[earlyWarning.level]}
            </span>
          )}
        </div>
        <p className="asset-comparison-card__muted">
          Baseline durumu: {insufficientBaseline ? 'Yetersiz geçmiş veri' : 'Yeterli geçmiş veri'}
        </p>
      </section>

      <p className="asset-comparison-card__explanation">
        İnceleme Önceliği bakım iş yükünü, Erken Uyarı ise varlığın kendi geçmişinden sapmayı gösterir.
        Bu göstergeler arıza olasılığı değildir.
      </p>

      <dl className="asset-comparison-card__timestamps">
        <ComparisonField label="Analiz tarihi" value={formatOptionalDate(data.asOf)} />
        <ComparisonField label="Yanıt üretim zamanı" value={formatOptionalDate(data.generatedAt, true)} />
      </dl>

      <Link className="btn btn-outline-secondary asset-comparison-card__detail" to={`/assets/${identity.assetId}`}>
        Asset 360 detayını aç
      </Link>
    </article>
  )
}

function ComparisonField({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value?.trim() || 'Bilgi bulunmuyor'}</dd>
    </div>
  )
}

function ComparisonMetric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

function formatOptionalDate(value: string | null, includeTime = false): string {
  if (!value) return 'Bilgi bulunmuyor'

  const normalized = /^\d{4}-\d{2}-\d{2}$/.test(value) ? `${value}T00:00:00` : value
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) return 'Bilgi bulunmuyor'

  return new Intl.DateTimeFormat('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    ...(includeTime ? { hour: '2-digit', minute: '2-digit' } : {}),
  }).format(date)
}
