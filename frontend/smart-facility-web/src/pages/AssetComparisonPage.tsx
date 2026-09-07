import { useEffect, useMemo, useState } from 'react'
import { useQueries } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import type { AssetSearchItem } from '../api/analyticsTypes'
import { AssetComparisonCard } from '../components/AssetComparisonCard'
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
    updateSelection(removeAssetComparisonId(assetIds, assetId))
    setAnnouncement(`Varlık ${assetId} karşılaştırmadan kaldırıldı.`)
  }

  const clearSelection = () => {
    updateSelection(clearAssetComparisonIds())
    setAnnouncement('Tüm varlık seçimleri temizlendi.')
  }

  const remainingCount = Math.max(0, 2 - assetIds.length)
  const asOfValues = summaries
    .map((summary) => summary.data?.asOf ?? null)
    .filter((_, index) => summaries[index].data !== undefined)
  const hasDifferentAsOf = asOfValues.length > 1 && new Set(asOfValues).size > 1

  return (
    <div className="page-stack page-stack--asset-comparison">
      <PageHeader
        eyebrow="Varlık analitiği"
        title="Varlık Karşılaştırma"
        description="İki veya üç varlığın güncel bakım aktivitesi ile karar destek göstergelerini seçim sırasına göre yan yana inceleyin."
        actions={<Link className="btn btn-outline-secondary" to="/assets">← Varlıklara dön</Link>}
      />

      <AssetSearch
        onSelect={addAsset}
        selectedAssetIds={assetIds}
        selectionLimit={maximumComparedAssets}
      />

      <section className="asset-comparison-selection" aria-labelledby="comparison-selection-title">
        <div>
          <h2 id="comparison-selection-title">Seçilen varlıklar</h2>
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
