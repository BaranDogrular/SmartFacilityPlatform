import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { AnalyticsApiError } from '../api/analyticsClient'
import type {
  AssetActivityHistoricalIntervention,
  AssetActivityItem,
  AssetActivityResponse,
  AssetActivityState,
} from '../api/analyticsTypes'
import { useAssetActivity } from '../hooks/useAnalytics'
import { EmptyState, ErrorState, InfoNote, LoadingState } from './DashboardUi'

const activityPageSize = 25
const maximumCursorHistory = 50

interface CursorHistoryEntry {
  cursor: string | null
  pageNumber: number
}

const stateLabels: Record<AssetActivityState, string> = {
  OPEN: 'AÇIK',
  CLOSED: 'KAPALI',
  OTHER: 'DİĞER',
}

const interventionQualityLabels = {
  INFORMATIVE: 'Bilgilendirici',
  GENERIC: 'Genel',
  NO_ACTION: 'İşlem bilgisi yok',
} as const

const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export function AssetActivityTimeline({ assetId }: { assetId: number }) {
  const [cursorHistory, setCursorHistory] = useState<CursorHistoryEntry[]>([
    { cursor: null, pageNumber: 1 },
  ])
  const currentCursor = cursorHistory[cursorHistory.length - 1]
  const activity = useAssetActivity(assetId, currentCursor.cursor, activityPageSize)
  const [visiblePage, setVisiblePage] = useState<AssetActivityResponse | null>(
    () => activity.data ?? null,
  )
  const [visiblePageNumber, setVisiblePageNumber] = useState(currentCursor.pageNumber)
  const [expandedWorkOrders, setExpandedWorkOrders] = useState<Set<number>>(() => new Set())
  const [snapshotRefreshNotice, setSnapshotRefreshNotice] = useState(false)
  const staleCursor = activity.error instanceof AnalyticsApiError && activity.error.status === 409

  useEffect(() => {
    if (!activity.data || activity.data.assetId !== assetId) return

    setVisiblePage(activity.data)
    setVisiblePageNumber(currentCursor.pageNumber)
    setExpandedWorkOrders(new Set())
  }, [activity.data, assetId, currentCursor.pageNumber])

  useEffect(() => {
    if (!staleCursor || currentCursor.cursor === null) return

    setCursorHistory([{ cursor: null, pageNumber: 1 }])
    setVisiblePage(null)
    setVisiblePageNumber(1)
    setExpandedWorkOrders(new Set())
    setSnapshotRefreshNotice(true)
  }, [staleCursor, currentCursor.cursor])

  const goNext = () => {
    if (!visiblePage?.hasNextPage || !visiblePage.nextCursor || activity.isFetching) return

    const entry: CursorHistoryEntry = {
      cursor: visiblePage.nextCursor,
      pageNumber: visiblePageNumber + 1,
    }
    setCursorHistory((current) => [...current, entry].slice(-maximumCursorHistory))
    setSnapshotRefreshNotice(false)
  }

  const goPrevious = () => {
    if (cursorHistory.length <= 1 || activity.isFetching) return

    setCursorHistory((current) => current.slice(0, -1))
    setSnapshotRefreshNotice(false)
  }

  const toggleIntervention = (workOrderId: number) => {
    setExpandedWorkOrders((current) => {
      const next = new Set(current)
      if (next.has(workOrderId)) next.delete(workOrderId)
      else next.add(workOrderId)
      return next
    })
  }

  return (
    <section className="asset-activity" aria-labelledby="asset-activity-title">
      <header className="asset-activity__header">
        <div>
          <p className="page-eyebrow">Canonical bakım aktivitesi</p>
          <h2 id="asset-activity-title">Varlık İş Emri Geçmişi</h2>
          <p>
            Bu varlıkla doğrulanmış şekilde eşleşen güncel iş emirleri, en yeniden en eskiye
            sıralanır.
          </p>
        </div>
        <span className="asset-activity__page" aria-live="polite">
          Sayfa {visiblePageNumber} · En fazla {activityPageSize} kayıt
        </span>
      </header>

      {snapshotRefreshNotice ? (
        <div className="asset-activity__notice" role="alert">
          Veri seti yenilendi. Güncel kayıtlarla güvenli biçimde ilk sayfaya dönüldü.
        </div>
      ) : null}

      {!visiblePage && activity.isPending ? (
        <LoadingState label="Varlık iş emri geçmişi yükleniyor" />
      ) : null}

      {!visiblePage && activity.error && !staleCursor ? (
        <ErrorState error={activity.error} onRetry={() => void activity.refetch()} />
      ) : null}

      {visiblePage && activity.error && !staleCursor ? (
        <div className="asset-activity__inline-error" role="alert">
          <strong>Yeni sayfa yüklenemedi.</strong>
          <span>Mevcut kayıtlar korunuyor.</span>
          <button className="btn btn-sm btn-outline-primary" type="button" onClick={() => void activity.refetch()}>
            Yeniden dene
          </button>
        </div>
      ) : null}

      {visiblePage?.items.length === 0 ? (
        <EmptyState message="Bu varlıkla eşleşen güncel iş emri bulunmuyor." />
      ) : null}

      {visiblePage && visiblePage.items.length > 0 ? (
        <div className="asset-activity__list" aria-busy={activity.isFetching}>
          {visiblePage.items.slice(0, activityPageSize).map((item) => (
            <ActivityItem
              key={item.workOrderId}
              assetId={assetId}
              item={item}
              expanded={expandedWorkOrders.has(item.workOrderId)}
              onToggle={() => toggleIntervention(item.workOrderId)}
            />
          ))}
        </div>
      ) : null}

      {visiblePage ? (
        <footer className="asset-activity__footer">
          <div className="asset-activity__pagination" aria-label="İş emri geçmişi sayfalama">
            <button
              className="btn btn-outline-secondary"
              type="button"
              onClick={goPrevious}
              disabled={cursorHistory.length <= 1 || activity.isFetching}
            >
              Önceki
            </button>
            <button
              className="btn btn-primary"
              type="button"
              onClick={goNext}
              disabled={
                !visiblePage.hasNextPage
                || !visiblePage.nextCursor
                || activity.isFetching
                || Boolean(activity.error)
              }
            >
              Sonraki
            </button>
          </div>
          {activity.isFetching ? <span role="status">Yeni sayfa yükleniyor…</span> : null}
        </footer>
      ) : null}

      {visiblePage ? (
        <InfoNote>
          Yalnızca sistemin güvenli gösterim için hazırladığı iş emri ve müdahale alanları sunulur.
          Toplam sayfa hesabı yapılmaz; aynı anda yalnız bir cursor sayfası gösterilir.
        </InfoNote>
      ) : null}
    </section>
  )
}

function ActivityItem({
  assetId,
  item,
  expanded,
  onToggle,
}: {
  assetId: number
  item: AssetActivityItem
  expanded: boolean
  onToggle: () => void
}) {
  const interventionRegionId = `asset-${assetId}-work-order-${item.workOrderId}-intervention`
  const metadata = [item.status, item.discipline, item.workType, item.failureType]
    .filter((value): value is string => Boolean(value?.trim()))

  return (
    <article className="asset-activity-item">
      <header>
        <div className="asset-activity-item__identity">
          <time dateTime={item.reportedDateTime ?? undefined}>
            {formatDateTime(item.reportedDateTime)}
          </time>
          <strong>{item.workOrderNumber}</strong>
        </div>
        <span className={`activity-state activity-state--${item.state.toLowerCase()}`}>
          {stateLabels[item.state]}
        </span>
      </header>

      {metadata.length > 0 ? (
        <div className="asset-activity-item__metadata" aria-label="İş emri sınıflandırması">
          {metadata.map((value, index) => <span key={`${value}-${index}`}>{value}</span>)}
        </div>
      ) : null}

      {item.descriptionSnippet ? (
        <p className="asset-activity-item__description">{item.descriptionSnippet}</p>
      ) : null}

      <div className="asset-activity-item__actions">
        {item.historicalIntervention ? (
          <button
            className="asset-activity-item__toggle"
            type="button"
            aria-expanded={expanded}
            aria-controls={interventionRegionId}
            onClick={onToggle}
          >
            {expanded ? 'Müdahale detayını kapat' : 'Müdahale detayını göster'}
          </button>
        ) : (
          <span className="asset-activity-item__missing-intervention">
            Geçmiş müdahale kaydı bulunmuyor
          </span>
        )}
        <Link
          className="btn btn-sm btn-outline-primary"
          to={`/work-orders/${item.workOrderId}/similar-cases`}
          state={{ originAssetId: assetId }}
        >
          Benzer Vakaları Gör
        </Link>
      </div>

      {item.historicalIntervention ? (
        <div id={interventionRegionId} hidden={!expanded}>
          <InterventionDetails
            intervention={item.historicalIntervention}
            interventionCount={item.interventionCount}
          />
        </div>
      ) : null}
    </article>
  )
}

function InterventionDetails({
  intervention,
  interventionCount,
}: {
  intervention: AssetActivityHistoricalIntervention
  interventionCount: number
}) {
  return (
    <section className="asset-activity-intervention" aria-label="Güvenli geçmiş müdahale bilgisi">
      <header>
        <h3>Gözlenen geçmiş müdahale</h3>
        <span>{interventionQualityLabels[intervention.quality]}</span>
      </header>
      <dl>
        {intervention.requestDescription ? (
          <div><dt>Talep açıklaması</dt><dd>{intervention.requestDescription}</dd></div>
        ) : null}
        {intervention.failureReasonDescription ? (
          <div><dt>Arıza nedeni</dt><dd>{intervention.failureReasonDescription}</dd></div>
        ) : null}
        {intervention.workPerformedDescription ? (
          <div><dt>Gerçekleştirilen işlem</dt><dd>{intervention.workPerformedDescription}</dd></div>
        ) : null}
        {intervention.observedCompletionDateTime ? (
          <div>
            <dt>Gözlenen müdahale tamamlanma zamanı</dt>
            <dd>{formatDateTime(intervention.observedCompletionDateTime)}</dd>
          </div>
        ) : null}
        {interventionCount > 1 ? (
          <div><dt>Eşleşen müdahale kaydı</dt><dd>{interventionCount}</dd></div>
        ) : null}
      </dl>
      <p>Yalnızca sistemin güvenli gösterim için hazırladığı müdahale alanları sunulur.</p>
    </section>
  )
}

function formatDateTime(value: string | null): string {
  if (!value) return 'Tarih bilgisi bulunmuyor'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : dateTimeFormatter.format(parsed)
}
