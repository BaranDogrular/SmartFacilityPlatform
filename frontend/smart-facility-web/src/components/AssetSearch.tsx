import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { AssetSearchItem } from '../api/analyticsTypes'
import { useAssetSearch } from '../hooks/useAnalytics'

const searchResultLimit = 10
const searchDebounceMilliseconds = 300

interface AssetSearchProps {
  initialQuery?: string
  onSelect?: (asset: AssetSearchItem) => void
  selectedAssetIds?: readonly number[]
  selectionLimit?: number
}

export function AssetSearch({
  initialQuery = '',
  onSelect,
  selectedAssetIds = [],
  selectionLimit = 3,
}: AssetSearchProps) {
  const [input, setInput] = useState(initialQuery)
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery.trim())
  const normalizedInput = input.trim()
  const canSearch = normalizedInput.length >= 2 && normalizedInput.length <= 100
  const waitingForDebounce = canSearch && normalizedInput !== debouncedQuery
  const search = useAssetSearch(
    debouncedQuery,
    searchResultLimit,
    debouncedQuery.length >= 2 && debouncedQuery.length <= 100,
  )
  const showCurrentSearch = canSearch && normalizedInput === debouncedQuery

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedQuery(normalizedInput)
    }, searchDebounceMilliseconds)

    return () => window.clearTimeout(timer)
  }, [normalizedInput])

  const submitSearch = (event: FormEvent) => {
    event.preventDefault()
    if (canSearch) setDebouncedQuery(normalizedInput)
  }

  const clearSearch = () => {
    setInput('')
    setDebouncedQuery('')
  }

  return (
    <section className="asset-search" aria-labelledby="asset-search-title">
      <header>
        <div>
          <p className="page-eyebrow">Varlık keşfi</p>
          <h2 id="asset-search-title">Varlık Ara</h2>
          <p>Mevcut bir varlığa kodu veya adı üzerinden doğrudan ulaşın.</p>
        </div>
        <span>En fazla {searchResultLimit} sonuç</span>
      </header>

      <form className="asset-search__form" role="search" onSubmit={submitSearch}>
        <label htmlFor="asset-search-input">Varlık kodu veya adı</label>
        <div>
          <input
            id="asset-search-input"
            className="form-control"
            type="search"
            maxLength={100}
            autoComplete="off"
            placeholder="Varlık kodu veya adıyla ara"
            value={input}
            onChange={(event) => setInput(event.target.value)}
          />
          <button className="btn btn-primary" type="submit" disabled={!canSearch || search.isFetching}>
            Ara
          </button>
          <button
            className="btn btn-outline-secondary"
            type="button"
            onClick={clearSearch}
            disabled={input.length === 0}
          >
            Temizle
          </button>
        </div>
      </form>

      <div className="asset-search__status" aria-live="polite">
        {!canSearch ? <p>Arama yapmak için en az 2 karakter girin.</p> : null}
        {waitingForDebounce ? <p role="status">Arama hazırlanıyor…</p> : null}
        {showCurrentSearch && search.isPending ? <p role="status">Varlıklar aranıyor…</p> : null}
        {showCurrentSearch && search.error ? (
          <div className="asset-search__error" role="alert">
            <span>Arama sonuçları alınamadı.</span>
            <button className="btn btn-sm btn-outline-primary" type="button" onClick={() => void search.refetch()}>
              Yeniden dene
            </button>
          </div>
        ) : null}
        {showCurrentSearch && search.data?.length === 0 ? (
          <p>Aramanızla eşleşen varlık bulunamadı.</p>
        ) : null}
      </div>

      {showCurrentSearch && search.data && search.data.length > 0 ? (
        <ul className="asset-search__results" aria-label="Varlık arama sonuçları">
          {search.data.map((item) => {
            const context = [item.buildingName, item.locationName, item.assetGroupName]
              .filter((value): value is string => Boolean(value?.trim()))
            const isSelected = selectedAssetIds.includes(item.assetId)
            const selectionLimitReached = selectedAssetIds.length >= selectionLimit
            const content = (
              <>
                <span className="code-chip">{item.assetCode}</span>
                <strong>{item.assetName}</strong>
                {context.length > 0 ? <small>{context.join(' · ')}</small> : null}
              </>
            )
            return (
              <li key={item.assetId}>
                {onSelect ? (
                  <div className="asset-search__selection-result">
                    <div>{content}</div>
                    <button
                      className="btn btn-sm btn-outline-primary"
                      type="button"
                      disabled={isSelected || selectionLimitReached}
                      onClick={() => onSelect(item)}
                    >
                      {isSelected
                        ? 'Seçildi'
                        : selectionLimitReached
                          ? `En fazla ${selectionLimit} varlık`
                          : 'Karşılaştırmaya ekle'}
                    </button>
                  </div>
                ) : (
                  <Link to={`/assets/${item.assetId}`}>{content}</Link>
                )}
              </li>
            )
          })}
        </ul>
      ) : null}
    </section>
  )
}
