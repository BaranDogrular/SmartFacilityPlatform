import { ChartPanel, HorizontalBarChart } from './AnalyticsCharts'
import { EmptyState, ErrorState, InfoNote, LoadingState } from './DashboardUi'
import { useAssetMaintenanceActivityPareto } from '../hooks/useAnalytics'
import { formatCount, formatPercent } from '../utils/format'

const top = 10

export function AssetMaintenanceActivityPareto() {
  const query = useAssetMaintenanceActivityPareto({ top })

  if (query.isPending) {
    return <LoadingState label="Asset bakım aktivitesi yükleniyor" />
  }

  if (query.error) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />
  }

  if (!query.data) {
    return <EmptyState />
  }

  const data = query.data

  return (
    <ChartPanel
      title="Current İş Emri Aktivitesi En Yoğun Asset'ler"
      subtitle={`Top-${data.appliedTop} asset'in toplam current iş emri kayıtlarındaki payı`}
      reliability={data.metadata.reliability}
    >
      {data.topAssets.length === 0 ? (
        <EmptyState message="Seçilen kapsamda current iş emri aktivitesi bulunan asset yok." />
      ) : (
        <>
          <HorizontalBarChart
            data={data.topAssets.map((item) => ({
              label: item.assetCode,
              count: item.currentWorkOrderCount,
            }))}
            maxItems={data.appliedTop}
          />

          <div className="quality-summary" aria-label="Current iş emri aktivitesi özeti">
            <span>Toplam current kayıt <strong>{formatCount(data.totalCurrentWorkOrders)}</strong></span>
            <span>Current kaydı olan asset <strong>{formatCount(data.assetsWithCurrentWorkOrders)}</strong></span>
          </div>

          <div className="table-responsive pareto-table-wrap">
            <table className="analytics-table">
              <thead>
                <tr>
                  <th scope="col">Sıra</th>
                  <th scope="col">Asset kodu</th>
                  <th scope="col">Asset</th>
                  <th scope="col" className="text-end">Current iş emri</th>
                  <th scope="col" className="text-end">Pay</th>
                  <th scope="col" className="text-end">Kümülatif pay</th>
                </tr>
              </thead>
              <tbody>
                {data.topAssets.map((item, index) => (
                  <tr key={item.assetId}>
                    <td>{index + 1}</td>
                    <td><span className="code-chip">{item.assetCode}</span></td>
                    <td>{item.assetName}</td>
                    <td className="text-end"><strong>{formatCount(item.currentWorkOrderCount)}</strong></td>
                    <td className="text-end">{formatPercent(item.sharePercent)}</td>
                    <td className="text-end"><strong>{formatPercent(item.cumulativeSharePercent)}</strong></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      <InfoNote>
        Yüksek kayıt aktivitesi, asset'in sağlık durumu veya arızaya yatkınlığı hakkında bir sonuç değildir.
      </InfoNote>
    </ChartPanel>
  )
}
