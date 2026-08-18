const numberFormatter = new Intl.NumberFormat('tr-TR')
const monthFormatter = new Intl.DateTimeFormat('tr-TR', {
  month: 'short',
  year: 'numeric',
})

export const formatCount = (value: number): string => numberFormatter.format(value)

export const formatMonth = (value: string): string => {
  const date = new Date(`${value.slice(0, 10)}T00:00:00`)
  return Number.isNaN(date.getTime()) ? value : monthFormatter.format(date)
}

export const getErrorMessage = (error: unknown): string =>
  error instanceof Error ? error.message : 'Veriler alınırken beklenmeyen bir sorun oluştu.'
