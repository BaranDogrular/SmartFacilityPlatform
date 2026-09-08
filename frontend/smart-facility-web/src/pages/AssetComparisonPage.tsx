import { useEffect, useMemo, useRef, useState } from 'react'
import { useQueries } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import type { AssetSearchItem } from '../api/analyticsTypes'
import { AssetComparisonCard } from '../components/AssetComparisonCard'
import { AssetComparisonPrintButton } from '../components/AssetComparisonPrintButton'
import { AssetSearch } from '../components/AssetSearch'
import { InfoNote, PageHeader } from '../components/DashboardUi'
import { asset360SummaryQueryOptions } from '../hooks/useAnalytics'
import {
  addAssetComparisonId,
  clearAssetComparisonIds,
  getAssetComparisonValidationMessage,
  maximumComparedAssets,
  parseAssetComparisonSearch,
  removeAssetComparisonId,
  serializeAssetComparisonIds,
} from '../utils/assetComparison'

export function AssetComparisonPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const parsed = useMemo(() => parseAssetComparisonSearch(searchParams), [searchParams])
  const assetIds = parsed.ids
  const selectionHeadingRef = useRef<HTMLHeadingElement>(null)
  const shouldRestoreSelectionFocus = useRef(false)
  const [reportGeneratedAt] = useState(() => new Date())
  const [announcement, setAnnouncement] = useState<string | null>(
    () => getAssetComparisonValidationMessage(parsed),
  )
  const summaries = useQueries({
    queries: assetIds.map((assetId) => ({
      ...asset360SummaryQueryOptions(assetId),
      retry: false,
    })),
  })

  useEffect(() => {
    const normalized = createSearchParamsWithIds(searchParams, assetIds)
    if (normalized.toString() !== searchParams.toString()) {
      const validationMessage = getAssetComparisonValidationMessage(parsed)
      if (validationMessage) setAnnouncement(validationMessage)
      setSearchParams(normalized, { replace: true })
    }
  }, [assetIds, parsed, searchParams, setSearchParams])

  useEffect(() => {
    if (shouldRestoreSelectionFocus.current) {
      selectionHeadingRef.current?.focus()
      shouldRestoreSelectionFocus.current = false
    }
  }, [assetIds])

  const updateSelection = (nextIds: readonly number[]) => {
    setSearchParams(createSearchParamsWithIds(searchParams, nextIds))
  }

  const addAsset = (asset: AssetSearchItem) => {
    if (assetIds.includes(asset.assetId)) {
      setAnnouncement(`${asset.assetCode} zaten karşılaştırmada.`)
      return
    }
    if (assetIds.length >= maximumComparedAssets) {
      setAnnouncement(`En fazla ${maximumComparedAssets} varlık karşılaştırılabilir.`)
      return
    }

    updateSelection(addAssetComparisonId(assetIds, asset.assetId))
    setAnnouncement(`${asset.assetCode} karşılaştırmaya eklendi.`)
  }

  const removeAsset = (assetId: number) => {
    shouldRestoreSelectionFocus.current = true
    updateSelection(removeAssetComparisonId(assetIds, assetId))
    setAnnouncement(`Varlık ${assetId} karşılaştırmadan kaldırıldı.`)
  }

  const clearSelection = () => {
    shouldRestoreSelectionFocus.current = true
    updateSelection(clearAssetComparisonIds())
    setAnnouncement('Tüm varlık seçimleri temizlendi.')
  }

  const remainingCount = Math.max(0, 2 - assetIds.length)
  const asOfValues = summaries
    .map((summary) => summary.data?.asOf ?? null)
    .filter((_, index) => summaries[index].data !== undefined)
  const hasDifferentAsOf = asOfValues.length > 1 && new Set(asOfValues).size > 1
  const hasRequiredSelection = assetIds.length >= 2 && assetIds.length <= maximumComparedAssets
  const hasSummaryFailure = summaries.some((summary) => summary.error)
  const allSummariesReady = hasRequiredSelection
    && summaries.length === assetIds.length
    && summaries.every((summary) => summary.data && !summary.isPending && !summary.isFetching && !summary.error)
  const printDisabledReason = allSummariesReady
    ? null
    : !hasRequiredSelection
      ? `Rapor için en az 2, en fazla ${maximumComparedAssets} varlık seçilmelidir.`
      : hasSummaryFailure
        ? 'Rapor için bütün varlık özetleri başarıyla yüklenmelidir.'
        : 'Rapor için varlık özetlerinin yüklenmesi bekleniyor.'

  return (
    <div className="page-stack page-stack--asset-comparison">
      <PageHeader
        eyebrow="Varlık analitiği"
        title="Varlık Karşılaştırma Raporu"
        description="İki veya üç varlığın güncel bakım aktivitesi ile karar destek göstergelerini seçim sırasına göre yan yana inceleyin."
        actions={(
          <div className="asset-comparison-report-actions comparison-screen-only">
            <Link className="btn btn-outline-secondary" to="/assets">← Varlıklara dön</Link>
            <AssetComparisonPrintButton
              enabled={allSummariesReady}
              disabledReason={printDisabledReason}
            />
          </div>
        )}
      />

      <dl className="asset-comparison-report-meta" aria-label="Rapor bilgileri">
        <div>
          <dt>Rapora dahil edilen varlık</dt>
          <dd>{assetIds.length}</dd>
        </div>
        <div>
          <dt>Rapor görünümü oluşturuldu</dt>
          <dd>{formatReportGeneratedAt(reportGeneratedAt)}</dd>
        </div>
      </dl>

      <AssetSearch
        onSelect={addAsset}
        selectedAssetIds={assetIds}
        selectionLimit={maximumComparedAssets}
      />

      <section className="asset-comparison-selection" aria-labelledby="comparison-selection-title">
        <div>
          <h2 id="comparison-selection-title" ref={selectionHeadingRef} tabIndex={-1}>Seçilen varlıklar</h2>
          <p>{assetIds.length} / {maximumComparedAssets} varlık seçildi.</p>
        </div>
        {assetIds.length > 0 ? (
          <button className="btn btn-outline-secondary" type="button" onClick={clearSelection}>
            Tüm seçimleri temizle
          </button>
        ) : null}
      </section>

      <div className="asset-comparison-announcement" aria-live="polite">
        {announcement ? <p>{announcement}</p> : null}
        {remainingCount > 0 ? (
          <p>Karşılaştırmayı başlatmak için {remainingCount} varlık daha seçin.</p>
        ) : null}
        {assetIds.length === maximumComparedAssets ? (
          <p>Seçim limiti doldu: en fazla {maximumComparedAssets} varlık karşılaştırılabilir.</p>
        ) : null}
      </div>

      {hasDifferentAsOf ? (
        <div className="asset-comparison-snapshot-warning" role="status">
          Kartların analiz tarihleri farklıdır; değerleri karşılaştırırken her kartın tarihini ayrıca dikkate alın.
        </div>
      ) : null}

      {assetIds.length > 0 ? (
        <section
          className={`asset-comparison-grid asset-comparison-grid--${assetIds.length}`}
          aria-label="Varlık karşılaştırma kartları"
        >
          {assetIds.map((assetId, index) => {
            const summary = summaries[index]
            return (
              <AssetComparisonCard
                key={assetId}
                assetId={assetId}
                data={summary.data}
                error={summary.error}
                isPending={summary.isPending}
                isFetching={summary.isFetching}
                onRetry={() => void summary.refetch()}
                onRemove={() => removeAsset(assetId)}
              />
            )
          })}
        </section>
      ) : null}

      <InfoNote>
        URL yalnız varlık seçimlerini korur; gösterilen verileri veya tek bir snapshot’ı sabitlemez.
        İnceleme Önceliği ve Erken Uyarı farklı göstergelerdir; sonuçlar arıza olasılığı değildir.
      </InfoNote>
    </div>
  )
}

function createSearchParamsWithIds(
  current: URLSearchParams,
  assetIds: readonly number[],
): URLSearchParams {
  const next = new URLSearchParams(current)
  next.delete('ids')
  const serialized = serializeAssetComparisonIds(assetIds)
  if (serialized) next.set('ids', serialized)
  return next
}

function formatReportGeneratedAt(value: Date): string {
  return new Intl.DateTimeFormat('tr-TR', {
    dateStyle: 'long',
    timeStyle: 'short',
  }).format(value)
}
