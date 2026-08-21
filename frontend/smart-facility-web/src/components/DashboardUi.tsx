import type { ReactNode } from 'react'
import type { KpiReliability } from '../api/analyticsTypes'
import { formatCount, getErrorMessage } from '../utils/format'

interface PageHeaderProps {
  eyebrow: string
  title: string
  description: string
  actions?: ReactNode
}

export function PageHeader({ eyebrow, title, description, actions }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div>
        <p className="page-eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        <p className="page-description">{description}</p>
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
  )
}

interface KpiCardProps {
  label: string
  value: number | string
  note?: string
  reliability?: KpiReliability
  tone?: 'navy' | 'teal' | 'amber' | 'slate'
}

export function KpiCard({
  label,
  value,
  note,
  reliability,
  tone = 'navy',
}: KpiCardProps) {
  return (
    <article className={`kpi-card kpi-card--${tone}`}>
      <div className="kpi-card__header">
        <span>{label}</span>
        {reliability ? <ReliabilityBadge reliability={reliability} compact /> : null}
      </div>
      <strong className="kpi-card__value">
        {typeof value === 'number' ? formatCount(value) : value}
      </strong>
      {note ? <p className="kpi-card__note">{note}</p> : null}
    </article>
  )
}

interface ReliabilityBadgeProps {
  reliability: KpiReliability
  compact?: boolean
}

export function ReliabilityBadge({ reliability, compact = false }: ReliabilityBadgeProps) {
  const isYellow = reliability === 'Yellow'
  const label = isYellow ? 'YELLOW · Veri kalitesi notu' : 'GREEN · Doğrulanmış metrik'
  const detail = isYellow
    ? 'Bu gösterge kaynak veri kapsamı veya eşleştirme kalitesi nedeniyle dikkatle yorumlanmalıdır.'
    : 'Bu gösterge doğrulanmış production veri sözleşmesini kullanır.'

  if (reliability === 'Red') {
    return null
  }

  return (
    <span
      className={`reliability-badge reliability-badge--${isYellow ? 'yellow' : 'green'}${compact ? ' reliability-badge--compact' : ''}`}
      title={detail}
    >
      <span aria-hidden="true">{isYellow ? 'i' : '✓'}</span>
      {compact ? <span className="visually-hidden">{label}</span> : label}
    </span>
  )
}

export function LoadingState({ label = 'Dashboard verileri yükleniyor' }: { label?: string }) {
  return (
    <div className="state-panel" role="status" aria-live="polite">
      <span className="loading-spinner" aria-hidden="true" />
      <div>
        <strong>{label}</strong>
        <p>Gerçek analytics verileri hazırlanıyor.</p>
      </div>
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="state-panel state-panel--error" role="alert">
      <span className="state-panel__icon" aria-hidden="true">!</span>
      <div>
        <strong>Veriler görüntülenemedi</strong>
        <p>{getErrorMessage(error)}</p>
        {onRetry ? (
          <button className="btn btn-sm btn-outline-primary" type="button" onClick={onRetry}>
            Yeniden dene
          </button>
        ) : null}
      </div>
    </div>
  )
}

export function EmptyState({ message = 'Seçilen filtrelerle eşleşen kayıt bulunamadı.' }) {
  return (
    <div className="state-panel state-panel--empty" role="status">
      <span className="state-panel__icon" aria-hidden="true">—</span>
      <div>
        <strong>Kayıt bulunamadı</strong>
        <p>{message}</p>
      </div>
    </div>
  )
}

export function InfoNote({ children }: { children: ReactNode }) {
  return (
    <div className="info-note">
      <span aria-hidden="true">i</span>
      <p>{children}</p>
    </div>
  )
}

export function DataTimestamp({ value }: { value: string }) {
  const parsed = new Date(value)
  const text = Number.isNaN(parsed.getTime())
    ? value
    : new Intl.DateTimeFormat('tr-TR', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(parsed)

  return <span className="data-timestamp">Veri görünümü: {text}</span>
}
