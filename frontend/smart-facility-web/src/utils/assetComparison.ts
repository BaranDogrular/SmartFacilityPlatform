export const maximumComparedAssets = 3

const maximumIdsQueryLength = 512
const maximumIdsSegments = 64

export interface AssetComparisonParseResult {
  ids: number[]
  invalidCount: number
  duplicateCount: number
  overLimitCount: number
  ignoredParameterCount: number
  inputWasTruncated: boolean
}

function parsePositiveSafeInteger(value: string): number | null {
  if (!/^\d+$/.test(value)) return null

  const parsed = Number(value)
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null
}

export function parseAssetComparisonSearch(
  search: string | URLSearchParams,
): AssetComparisonParseResult {
  const parameters = typeof search === 'string'
    ? new URLSearchParams(search.startsWith('?') ? search.slice(1) : search)
    : search
  const values = parameters.getAll('ids')
  const rawValue = values[0] ?? ''
  const boundedValue = rawValue.slice(0, maximumIdsQueryLength)
  const allSegments = boundedValue.split(',')
  const segments = allSegments.slice(0, maximumIdsSegments)
  const ids: number[] = []
  const seen = new Set<number>()
  let invalidCount = 0
  let duplicateCount = 0
  let overLimitCount = 0

  for (const segment of segments) {
    const parsed = parsePositiveSafeInteger(segment.trim())
    if (parsed === null) {
      if (segment.length > 0 || rawValue.length > 0) invalidCount += 1
      continue
    }

    if (seen.has(parsed)) {
      duplicateCount += 1
      continue
    }

    seen.add(parsed)
    if (ids.length >= maximumComparedAssets) {
      overLimitCount += 1
      continue
    }

    ids.push(parsed)
  }

  return {
    ids,
    invalidCount,
    duplicateCount,
    overLimitCount,
    ignoredParameterCount: Math.max(0, values.length - 1),
    inputWasTruncated:
      rawValue.length > maximumIdsQueryLength || allSegments.length > maximumIdsSegments,
  }
}

export function serializeAssetComparisonIds(ids: readonly number[]): string {
  return ids.slice(0, maximumComparedAssets).join(',')
}

export function addAssetComparisonId(ids: readonly number[], assetId: number): number[] {
  if (
    !Number.isSafeInteger(assetId)
    || assetId <= 0
    || ids.includes(assetId)
    || ids.length >= maximumComparedAssets
  ) {
    return [...ids]
  }

  return [...ids, assetId]
}

export function removeAssetComparisonId(ids: readonly number[], assetId: number): number[] {
  return ids.filter((id) => id !== assetId)
}

export function clearAssetComparisonIds(): number[] {
  return []
}

export function getAssetComparisonValidationMessage(
  result: AssetComparisonParseResult,
): string | null {
  const skippedCount = result.invalidCount
    + result.duplicateCount
    + result.overLimitCount
    + result.ignoredParameterCount

  if (skippedCount === 0 && !result.inputWasTruncated) return null

  const details: string[] = []
  if (result.invalidCount > 0) details.push(`${result.invalidCount} geçersiz kimlik`)
  if (result.duplicateCount > 0) details.push(`${result.duplicateCount} tekrar`)
  if (result.overLimitCount > 0) details.push(`${result.overLimitCount} limit üstü seçim`)
  if (result.ignoredParameterCount > 0) details.push('tekrarlanan ids parametresi')
  if (result.inputWasTruncated) details.push('çok uzun giriş')

  return `Bazı seçimler atlandı: ${details.join(', ')}.`
}
