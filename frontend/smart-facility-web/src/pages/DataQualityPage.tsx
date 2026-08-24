import { ChartPanel, HorizontalBarChart } from '../components/AnalyticsCharts'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'
import { useImportQualityOverview } from '../hooks/useAnalytics'

export function DataQualityPage() {
  const query = useImportQualityOverview()

  if (query.isPending) {
    return <LoadingState label="Import kalite görünümü hazırlanıyor" />
  }

  if (query.error) {
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />
  }

  if (!query.data) {
    return <EmptyState />
  }

  const data = query.data
  const statusCount = (status: string) => data.batchesByStatus.find((item) => item.category === status)?.count ?? 0

  return (
    <div className="page-stack page-stack--audit">
      <PageHeader eyebrow="Import denetimi" title="Veri Kalitesi ve Import Geçmişi" description="Import batch, source lineage ve fingerprint kapsamının teknik denetim görünümü." actions={<DataTimestamp value={data.metadata.dataAsOf} />} />

      <div className="audit-metric-groups">
        <section className="metric-group" aria-labelledby="batch-status-metrics-title">
          <header className="metric-group__header">
            <span>Batch operasyonu</span>
            <h2 id="batch-status-metrics-title">Import Durumu</h2>
          </header>
          <div className="kpi-grid kpi-grid--quality">
            <KpiCard label="Toplam Import Batch" value={data.totalBatches} reliability={data.metadata.reliability} />
            <KpiCard label="Tamamlanan Batch" value={statusCount('Completed')} reliability={data.metadata.reliability} tone="teal" />
            <KpiCard label="Başarısız Durum Kaydı" value={statusCount('Failed')} note="Import geçmişinde kayıtlı durum" reliability={data.metadata.reliability} tone="slate" />
            <KpiCard label="Devam Eden Durum Kaydı" value={statusCount('InProgress')} note="Import geçmişinde kayıtlı durum" reliability={data.metadata.reliability} tone="slate" />
          </div>
        </section>

        <section className="metric-group" aria-labelledby="lineage-metrics-title">
          <header className="metric-group__header">
            <span>Lineage ve audit</span>
            <h2 id="lineage-metrics-title">Source Record Kapsamı</h2>
          </header>
          <div className="kpi-grid kpi-grid--three">
            <KpiCard label="Import Denetim Kaydı" value={data.importErrorCount} note="Audit geçmişinde korunur" reliability={data.metadata.reliability} tone="amber" />
            <KpiCard label="Legacy Source Record" value={data.legacySourceRecordCount} reliability={data.metadata.reliability} tone="slate" />
            <KpiCard label="Versioned Source Record" value={data.versionedSourceRecordCount} reliability={data.metadata.reliability} tone="teal" />
          </div>
        </section>
      </div>

      <div className="dashboard-grid dashboard-grid--two">
        <ChartPanel title="Batch Durumları" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.batchesByStatus.map((item) => ({ label: item.category, count: item.count }))} />
        </ChartPanel>
        <ChartPanel title="Batch Kaynak Türleri" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.batchesBySourceType.map((item) => ({ label: item.sourceType, count: item.count }))} />
        </ChartPanel>
        <ChartPanel title="Source Record Parse Durumu" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.sourceRecordsByParseStatus.map((item) => ({ label: item.category, count: item.count }))} />
        </ChartPanel>
        <ChartPanel title="Fingerprint Algoritmaları" reliability={data.metadata.reliability}>
          <HorizontalBarChart data={data.fingerprintAlgorithmDistribution.map((item) => ({ label: item.category, count: item.count }))} />
        </ChartPanel>
      </div>

      <InfoNote>Başarısız ve devam eden durumlar import audit geçmişinin parçasıdır; otomatik olarak silinmesi gereken kayıtlar değildir.</InfoNote>
    </div>
  )
}
