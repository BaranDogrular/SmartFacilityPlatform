import { ChartPanel, HorizontalBarChart } from '../components/AnalyticsCharts'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader, ReliabilityBadge } from '../components/DashboardUi'
import { useAssetOverview } from '../hooks/useAnalytics'
import { formatCount } from '../utils/format'

export function AssetsPage() {
  const query = useAssetOverview({ top: 10 })

  if (query.isPending) {
    return <LoadingState label="Varlık analitiği yükleniyor" />
  }

  if (query.error) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />
  }

  if (!query.data || query.data.totalAssetCount === 0) {
    return <EmptyState />
  }

  const data = query.data

  return (
    <div className="page-stack">
      <PageHeader
        eyebrow="Varlık portföyü"
        title="Varlık görünümü"
        description="Operasyonel envanter dağılımı ve güncel iş emri ilişkisi."
        actions={<DataTimestamp value={data.metadata.dataAsOf} />}
      />

      <section className="kpi-grid kpi-grid--three" aria-label="Varlık göstergeleri">
        <KpiCard label="Toplam Varlık" value={data.totalAssetCount} reliability={data.metadata.reliability} />
        <KpiCard label="İş Emri Olan Varlık" value={data.assetsWithCurrentWorkOrders} reliability={data.metadata.reliability} tone="teal" />
        <KpiCard label="İş Emri Olmayan Varlık" value={data.assetsWithoutCurrentWorkOrders} reliability={data.metadata.reliability} tone="slate" />
      </section>

      <div className="dashboard-grid dashboard-grid--two">
        <ChartPanel title="Bina Bazında Varlık" subtitle="En yüksek varlık sayısına göre" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.countByBuilding.map((item) => ({ label: item.name, count: item.count }))} />
        </ChartPanel>
        <ChartPanel title="Varlık Grubu Bazında" subtitle="En yüksek 12 grup" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.countByAssetGroup.map((item) => ({ label: item.name, count: item.count }))} />
        </ChartPanel>
      </div>

      <ChartPanel title="Lokasyon Bazında Varlık" subtitle="En yüksek 15 lokasyon" reliability={data.metadata.reliability}>
        <HorizontalBarChart data={data.countByLocation.map((item) => ({ label: item.name, count: item.count }))} maxItems={15} />
      </ChartPanel>

      <section className="table-panel">
        <header className="chart-panel__header">
          <div>
            <h2>En Çok Güncel İş Emri Alan Varlıklar</h2>
            <p>Yalnız current WorkOrder ilişkileri üzerinden sıralanır.</p>
          </div>
          <ReliabilityBadge reliability={data.topAssetsReliability} />
        </header>
        {data.topAssetsByWorkOrderCount.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="table-responsive">
            <table className="analytics-table">
              <thead>
                <tr>
                  <th scope="col">Sıra</th>
                  <th scope="col">Varlık Kodu</th>
                  <th scope="col">Varlık</th>
                  <th scope="col" className="text-end">İş Emri</th>
                </tr>
              </thead>
              <tbody>
                {data.topAssetsByWorkOrderCount.map((item, index) => (
                  <tr key={item.assetId}>
                    <td>{index + 1}</td>
                    <td><span className="code-chip">{item.assetCode}</span></td>
                    <td>{item.assetName}</td>
                    <td className="text-end"><strong>{formatCount(item.workOrderCount)}</strong></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <InfoNote>“İş Emri Olmayan Varlık” metriği varlık sağlığı değerlendirmesi değildir.</InfoNote>
    </div>
  )
}
