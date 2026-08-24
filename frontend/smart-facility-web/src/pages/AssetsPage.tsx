import { ChartPanel, HorizontalBarChart } from '../components/AnalyticsCharts'
import { AssetMaintenanceActivityPareto } from '../components/AssetMaintenanceActivityPareto'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { useAssetOverview } from '../hooks/useAnalytics'

export function AssetsPage() {
  const query = useAssetOverview()

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
        title="Varlık portföyü"
        description="Operasyonel envanter dağılımı ve mevcut iş emri aktivitesi."
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

      <AssetMaintenanceActivityPareto />

      <InfoNote>“İş Emri Olmayan Varlık” metriği varlık sağlığı değerlendirmesi değildir.</InfoNote>
    </div>
  )
}
