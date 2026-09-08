interface AssetComparisonPrintButtonProps {
  enabled: boolean
  disabledReason: string | null
  onPrint?: () => void
}

export function AssetComparisonPrintButton({
  enabled,
  disabledReason,
  onPrint = () => window.print(),
}: AssetComparisonPrintButtonProps) {
  const reasonId = 'asset-comparison-print-reason'

  return (
    <div className="asset-comparison-print comparison-screen-only">
      <button
        className="btn btn-primary"
        type="button"
        disabled={!enabled}
        aria-describedby={!enabled && disabledReason ? reasonId : undefined}
        onClick={() => onPrint()}
      >
        Yazdır / PDF olarak kaydet
      </button>
      {!enabled && disabledReason ? (
        <p id={reasonId} role="status">{disabledReason}</p>
      ) : null}
    </div>
  )
}
