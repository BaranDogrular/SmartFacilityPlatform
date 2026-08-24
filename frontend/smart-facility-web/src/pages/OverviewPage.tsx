import { useAssetOverview, useImportQualityOverview, useScadaOverview, useWorkOrderOverview } from '../hooks/useAnalytics'
import { DataTimestamp, EmptyState, ErrorState, InfoNote, KpiCard, LoadingState, PageHeader } from '../components/DashboardUi'

export function OverviewPage() {
  const assets = useAssetOverview()
  const workOrders = useWorkOrderOverview()
  const scada = useScadaOverview()
  const importQuality = useImportQualityOverview()

  const isPending = assets.isPending || workOrders.isPending || scada.isPending || importQuality.isPending
  const error = assets.error ?? workOrders.error ?? scada.error ?? importQuality.error

  if (isPending) {
    return <LoadingState label="Genel görünüm hazırlanıyor" />
  }

  if (error) {
    return (
      <ErrorState
        error={error}
        onRetry={() => {
          void assets.refetch()
          void workOrders.refetch()
          void scada.refetch()
          void importQuality.refetch()
        }}
      />
    )
  }

  if (!assets.data || !workOrders.data || !scada.data || !importQuality.data) {
    return <EmptyState />
  }

  const total =
    assets.data.totalAssetCount +
    workOrders.data.totalWorkOrders +
    scada.data.totalAlarmOccurrences +
    importQuality.data.importErrorCount

  return (
    <div className="page-stack page-stack--overview">
      <PageHeader
        eyebrow="Operasyon özeti"
        title="Teknik operasyon özeti"
        description="Varlık, mevcut iş emri, SCADA alarm kaydı ve import denetim göstergelerinin bütüncül görünümü."
        actions={<DataTimestamp value={assets.data.metadata.dataAsOf} />}
      />

      {total === 0 ? <EmptyState /> : null}

      <section className="kpi-grid" aria-label="Genel performans göstergeleri">
        <KpiCard
          label="Toplam Varlık"
          value={assets.data.totalAssetCount}
          note="Operasyonel varlık envanteri"
          reliability={assets.data.metadata.reliability}
          tone="navy"
        />
        <KpiCard
          label="Güncel İş Emri"
          value={workOrders.data.totalWorkOrders}
          note="Yalnız current WorkOrder kaynağı"
          reliability={workOrders.data.metadata.reliability}
          tone="teal"
        />
        <KpiCard
          label="SCADA Alarm Kaydı"
          value={scada.data.totalAlarmOccurrences}
          note="Kaynak sistemdeki satır/occurrence sayısı"
          reliability={scada.data.metadata.reliability}
          tone="slate"
        />
        <KpiCard
          label="Import Denetim Kaydı"
          value={importQuality.data.importErrorCount}
          note="Import geçmişinde korunan hata kaydı"
          reliability={importQuality.data.metadata.reliability}
          tone="amber"
        />
      </section>

      <InfoNote>
        Bu ekran karar desteği için doğrulanmış özetleri gösterir. Import hata sayısı audit geçmişidir; aktif sistem alarmı anlamına gelmez.
      </InfoNote>
    </div>
  )
}
