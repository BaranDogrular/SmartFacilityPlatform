import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import type {
  SimilarCaseHistoricalIntervention,
  SimilarCasesQuery,
  SimilarCasesRetrievalMode,
} from '../api/analyticsTypes'
import { EmptyState, ErrorState, InfoNote, LoadingState, PageHeader } from '../components/DashboardUi'
import { useSimilarCases } from '../hooks/useAnalytics'
import { formatCount, formatPercent } from '../utils/format'

const dateTimeFormatter = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

function formatDateTime(value: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : dateTimeFormatter.format(date)
}

function retrievalLabel(mode: SimilarCasesRetrievalMode): string {
  if (mode === 'ASSET_GROUP_DISCIPLINE') return 'Varlık grubu + aynı disiplin'
  if (mode === 'NOT_AVAILABLE') return 'Bu kayıt için kullanılamıyor'
  return 'Aynı varlık + aynı disiplin'
}

function availabilityMessage(message: string | null): string {
  if (!message) return 'Yeterince benzer geçmiş vaka bulunamadı.'
  if (message.includes('linked AssetId')) return 'Bu kayıt bir varlığa bağlı olmadığı için benzer vaka analizi kullanılamıyor.'
  if (message.includes('description is too short')) return 'Kayıt açıklaması güvenilir karşılaştırma için çok kısa.'
  if (message.includes('no Discipline')) return 'Kayıtta disiplin bilgisi bulunmadığı için karşılaştırma yapılamıyor.'
  if (message.includes('no ReportedDateTime')) return 'Kayıtta bildirim tarihi bulunmadığı için temporal karşılaştırma yapılamıyor.'
  return 'Yeterince benzer geçmiş vaka bulunamadı.'
}

function HistoricalInterventionPanel({
  intervention,
}: {
  intervention: SimilarCaseHistoricalIntervention | null
}) {
  if (!intervention) {
    return (
      <section className="similar-case-intervention" aria-label="Geçmiş vakada yapılan işlem">
        <h3>Geçmiş Vakada Yapılan İşlem</h3>
        <p className="similar-case-intervention__empty">
          Bu geçmiş vaka için müdahale verisi bulunamadı.
        </p>
      </section>
    )
  }

  if (intervention.quality === 'NO_ACTION') {
    return (
      <section className="similar-case-intervention" aria-label="Geçmiş vakada yapılan işlem">
        <div className="similar-case-intervention__heading">
          <h3>Geçmiş Vakada Yapılan İşlem</h3>
          <span>Kayıt kalitesi: İşlem yok</span>
        </div>
        <p className="similar-case-intervention__empty">
          Bu geçmiş kayıt için anlamlı müdahale açıklaması bulunmuyor.
        </p>
      </section>
    )
  }

  return (
    <section className="similar-case-intervention" aria-label="Geçmiş vakada yapılan işlem">
      <div className="similar-case-intervention__heading">
        <h3>Geçmiş Vakada Yapılan İşlem</h3>
        <span>
          {intervention.quality === 'GENERIC'
            ? 'Kayıt kalitesi: Genel'
            : 'Kayıt kalitesi: Bilgilendirici'}
        </span>
      </div>
      <p className="similar-case-intervention__action">
        {intervention.workPerformedDescription ?? 'Müdahale açıklaması bulunmuyor.'}
      </p>
      {intervention.requestDescription
        || intervention.failureReasonDescription
        || intervention.completionDateTime ? (
          <dl className="similar-case-intervention__context">
            {intervention.requestDescription ? (
              <div><dt>Talep bağlamı</dt><dd>{intervention.requestDescription}</dd></div>
            ) : null}
            {intervention.failureReasonDescription ? (
              <div><dt>Arıza nedeni / bağlam</dt><dd>{intervention.failureReasonDescription}</dd></div>
            ) : null}
            {intervention.completionDateTime ? (
              <div><dt>Tamamlanma</dt><dd>{formatDateTime(intervention.completionDateTime)}</dd></div>
            ) : null}
          </dl>
        ) : null}
    </section>
  )
}

export function SimilarCasesPage() {
  const { id } = useParams()
  const workOrderId = Number(id)
  const validId = Number.isSafeInteger(workOrderId) && workOrderId > 0
  const [draftTop, setDraftTop] = useState('10')
  const [query, setQuery] = useState<SimilarCasesQuery>({ top: 10 })
  const [filterError, setFilterError] = useState('')
  const similarCases = useSimilarCases(workOrderId, query, validId)

  const applyTop = (event: FormEvent) => {
    event.preventDefault()
    const top = Number(draftTop)
    if (!Number.isInteger(top) || top < 1 || top > 50) {
      setFilterError('Top değeri 1 ile 50 arasında olmalıdır.')
      return
    }

    setFilterError('')
    setQuery({ top })
  }

  if (!validId) {
    return <EmptyState message="Geçerli bir canonical WorkOrder ID belirtilmedi." />
  }

  if (similarCases.isPending) {
    return <LoadingState label="Benzer geçmiş vakalar aranıyor" />
  }

  if (similarCases.error) {
    return <ErrorState error={similarCases.error} onRetry={() => void similarCases.refetch()} />
  }

  if (!similarCases.data) {
    return <EmptyState />
  }

  const { metadata, items } = similarCases.data

  return (
    <div className="page-stack similar-cases-page">
      <PageHeader
        eyebrow="Canonical WorkOrder geçmişi"
        title="Benzer Geçmiş Vakalar"
        description="Seçilen iş emrine benzer geçmiş bakım/talep kayıtlarını gösterir."
        actions={<Link className="btn btn-outline-secondary" to="/work-orders">İş Emirlerine dön</Link>}
      />

      <InfoNote>
        Bu bölüm çözüm önerisi veya otomatik bakım talimatı değildir. Benzerlik yüzdesi olasılık ya da model güven skoru değildir.
      </InfoNote>

      <section className="current-work-order-panel" aria-labelledby="current-work-order-title">
        <header>
          <div>
            <p className="page-eyebrow">Analiz hedefi</p>
            <h2 id="current-work-order-title">Seçilen İş Emri</h2>
          </div>
          <span className="code-chip">Canonical WorkOrder</span>
        </header>
        <div className="similar-cases-target" aria-label="Seçilen canonical iş emri">
          <div><span>WorkOrder ID</span><strong>{metadata.targetWorkOrderId}</strong></div>
          <div><span>Varlık</span><strong>{metadata.targetAsset.assetCode ?? 'Bağlı değil'}</strong><small>{metadata.targetAsset.assetName}</small></div>
          <div><span>Disiplin</span><strong>{metadata.targetDiscipline ?? '—'}</strong></div>
          <div><span>Bildirim tarihi</span><strong>{formatDateTime(metadata.targetReportedDateTime)}</strong></div>
        </div>
      </section>

      <form className="filter-panel similar-cases-filter" onSubmit={applyTop} aria-label="Benzer vaka filtreleri">
        <label>
          <span>Gösterilecek vaka</span>
          <input
            className="form-control"
            type="number"
            min="1"
            max="50"
            value={draftTop}
            onChange={(event) => setDraftTop(event.target.value)}
          />
        </label>
        <button className="btn btn-primary" type="submit">Uygula</button>
        {filterError ? <p className="filter-error" role="alert">{filterError}</p> : null}
      </form>

      <section className="similar-cases-metadata" aria-label="Retrieval bilgisi">
        <div><span>Arama modu</span><strong>{retrievalLabel(metadata.retrievalMode)}</strong></div>
        <div><span>Değerlendirilen aday</span><strong>{formatCount(metadata.candidateCount)}</strong></div>
        <div><span>Dönen vaka</span><strong>{formatCount(metadata.returnedCount)}</strong></div>
        <div><span>Tekrarlayan template bastırıldı</span><strong>{formatCount(metadata.duplicateTemplatesSuppressed)}</strong></div>
        <p>Temporal cutoff: yalnız {formatDateTime(metadata.temporalCutoff)} öncesindeki kayıtlar.</p>
      </section>

      {items.length === 0 ? (
        <EmptyState message={availabilityMessage(metadata.availabilityMessage)} />
      ) : (
        <section className="similar-cases-result-section" aria-labelledby="similar-cases-results-title">
          <header className="similar-cases-result-section__header">
            <div>
              <p className="page-eyebrow">Geçmiş vaka kanıtı</p>
              <h2 id="similar-cases-results-title">Benzer Geçmiş Vaka Sonuçları</h2>
              <p>Yalnız hedef iş emrinden önceki canonical kayıtlar listelenir.</p>
            </div>
            <span>{formatCount(metadata.returnedCount)} vaka</span>
          </header>
          <div className="similar-cases-results">
            {items.map((item) => (
            <article className="similar-case-card" key={item.workOrderId}>
              <header>
                <div>
                  <span className="similarity-score">Benzerlik {formatPercent(item.similarityScore)}</span>
                  <strong>{item.assetCode ?? 'Varlık bilgisi yok'} · {item.assetName ?? 'Tanımsız varlık'}</strong>
                </div>
                <time dateTime={item.reportedDateTime}>{formatDateTime(item.reportedDateTime)}</time>
              </header>
              <p className="similar-case-card__description">{item.descriptionSnippet}</p>
              <dl>
                <div><dt>Disiplin</dt><dd>{item.discipline ?? '—'}</dd></div>
                <div><dt>İş tipi</dt><dd>{item.workType ?? '—'}</dd></div>
                <div><dt>Bakım / arıza sınıfı</dt><dd>{item.failureType ?? '—'}</dd></div>
                <div><dt>İş emri</dt><dd>{item.workOrderNumber}</dd></div>
              </dl>
              <HistoricalInterventionPanel intervention={item.historicalIntervention} />
              <div className="similar-case-reasons" aria-label="Benzerlik nedenleri">
                {item.similarityReasons.map((reason) => <span key={reason}>{reason}</span>)}
              </div>
            </article>
            ))}
          </div>
        </section>
      )}

      <InfoNote>
        Problem benzerliği <span className="code-chip">core.WorkOrders</span>, gözlenen işlem bilgisi <span className="code-chip">core.HistoricalInterventions</span> kaynağındandır. Gösterilen işlemler bakım talimatı, önerilen veya garanti edilen çözüm değildir. Requester ve sorumlu personel alanları gösterilmez; aynı veya gelecekteki timestamp kayıtları aday değildir.
      </InfoNote>
    </div>
  )
}
