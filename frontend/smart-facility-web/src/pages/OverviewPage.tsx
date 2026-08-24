import type { ReactNode } from 'react'
import { ChartPanel, HorizontalBarChart } from '../components/AnalyticsCharts'
import {
  DataTimestamp,
  EmptyState,
  ErrorState,
  InfoNote,
  KpiCard,
  LoadingState,
  PageHeader,
  ReliabilityBadge,
} from '../components/DashboardUi'
import { useAssetOverview, useImportQualityOverview, useScadaOverview, useWorkOrderOverview } from '../hooks/useAnalytics'
import { formatCount } from '../utils/format'

function OperationalMetric({ label, value, note }: { label: string; value: number; note?: ReactNode }) {
  return (
    <div className="operational-metric">
      <span>{label}</span>
      <strong>{formatCount(value)}</strong>
      {note ? <small>{note}</small> : null}
    </div>
  )
}

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

  const importStatusCount = (status: string) =>
    importQuality.data.batchesByStatus.find((item) => item.category === status)?.count ?? 0

  return (
    <div className="page-stack page-stack--overview">
      <PageHeader
        eyebrow="Operasyon merkezi"
        title="Bakım ve Güvenilirlik Operasyon Özeti"
        description="Mevcut bakım aktivitesi, varlık yoğunluğu, SCADA veri bulguları ve import durumunun ortak karar destek görünümü."
        actions={<DataTimestamp value={assets.data.metadata.dataAsOf} />}
      />

      {total === 0 ? <EmptyState /> : null}

      <section className="overview-section overview-section--summary" aria-labelledby="operation-summary-title">
        <header className="overview-section__header">
          <div>
            <p className="page-eyebrow">Anlık operasyon kapsamı</p>
            <h2 id="operation-summary-title">Operasyon Özeti</h2>
          </div>
        </header>
        <div className="kpi-grid overview-kpi-strip" aria-label="Genel performans göstergeleri">
          <KpiCard
            label="Mevcut İş Emirleri"
            value={workOrders.data.totalWorkOrders}
            note="Yalnız current WorkOrder kaynağı"
            reliability={workOrders.data.metadata.reliability}
            tone="teal"
          />
          <KpiCard
            label="Operasyonel Varlıklar"
            value={assets.data.totalAssetCount}
            note={`${formatCount(assets.data.assetsWithCurrentWorkOrders)} varlıkta current aktivite`}
            reliability={assets.data.metadata.reliability}
            tone="navy"
          />
          <KpiCard
            label="SCADA Alarm Kaydı"
            value={scada.data.totalAlarmOccurrences}
            note="Kaynak satır/occurrence sayısı"
            reliability={scada.data.metadata.reliability}
            tone="slate"
          />
          <KpiCard
            label="Import Denetim Kaydı"
            value={importQuality.data.importErrorCount}
            note={`${formatCount(importQuality.data.totalBatches)} batch içinde korunan audit kaydı`}
            reliability={importQuality.data.metadata.reliability}
            tone="amber"
          />
        </div>
      </section>

      <section className="overview-section" aria-labelledby="primary-analytics-title">
        <header className="overview-section__header">
          <div>
            <p className="page-eyebrow">Bakım aktivitesi</p>
            <h2 id="primary-analytics-title">Ana Operasyon Analitiği</h2>
          </div>
          <p>Current WorkOrder kapsamındaki yoğunluk ve disiplin dağılımı.</p>
        </header>

        <div className="dashboard-grid dashboard-grid--two overview-primary-grid">
          <ChartPanel
            title="Bakım Aktivitesi Yoğunluğu"
            subtitle="Current iş emri sayısına göre öne çıkan varlıklar"
            reliability={assets.data.topAssetsReliability}
          >
            <HorizontalBarChart
              data={assets.data.topAssetsByWorkOrderCount.map((item) => ({
                label: item.assetCode,
                count: item.workOrderCount,
              }))}
              maxItems={6}
              compact
            />
            <div className="quality-summary">
              <span>Current kaydı olan varlık <strong>{formatCount(assets.data.assetsWithCurrentWorkOrders)}</strong></span>
            </div>
          </ChartPanel>

          <ChartPanel
            title="Mevcut İş Emri Disiplinleri"
            subtitle="Current WorkOrder kayıtlarının raw disiplin dağılımı"
            reliability={workOrders.data.metadata.reliability}
          >
            <HorizontalBarChart
              data={workOrders.data.byDiscipline.map((item) => ({ label: item.category, count: item.count }))}
              maxItems={6}
              compact
            />
            <div className="quality-summary">
              <span>Toplam current kayıt <strong>{formatCount(workOrders.data.totalWorkOrders)}</strong></span>
            </div>
          </ChartPanel>
        </div>
      </section>

      <section className="overview-section" aria-labelledby="secondary-status-title">
        <header className="overview-section__header">
          <div>
            <p className="page-eyebrow">Kontrol ve denetim</p>
            <h2 id="secondary-status-title">İkincil Operasyon Durumu</h2>
          </div>
          <p>SCADA kalite bulguları ve import audit geçmişi.</p>
        </header>

        <div className="overview-status-grid">
          <article className="operations-panel operations-panel--scada">
            <header className="operations-panel__header">
              <div>
                <span>SCADA</span>
                <h3>Veri Kalitesi Görünümü</h3>
              </div>
              <ReliabilityBadge reliability={scada.data.metadata.reliability} />
            </header>
            <div className="operations-panel__metrics">
              <OperationalMetric label="Toplam occurrence" value={scada.data.totalAlarmOccurrences} />
              <OperationalMetric label="Timestamp bulgusu" value={scada.data.invalidOrMissingTimestampCount} />
              <OperationalMetric label="Tarih kalitesi bulgusu" value={scada.data.dateQualityIssueCount} />
            </div>
            <p className="operations-panel__note">Occurrence sayısı benzersiz fiziksel alarm sayısı değildir. Clearance uygunluğu SCADA ekranında ayrı gösterilir.</p>
          </article>

          <article className="operations-panel operations-panel--import">
            <header className="operations-panel__header">
              <div>
                <span>IMPORT</span>
                <h3>Batch ve Audit Durumu</h3>
              </div>
              <ReliabilityBadge reliability={importQuality.data.metadata.reliability} />
            </header>
            <div className="operations-panel__metrics">
              <OperationalMetric label="Toplam batch" value={importQuality.data.totalBatches} />
              <OperationalMetric label="Tamamlanan" value={importStatusCount('Completed')} />
              <OperationalMetric label="Başarısız durum" value={importStatusCount('Failed')} />
            </div>
            <p className="operations-panel__note">Başarısız durum ve import hata sayıları korunan audit geçmişidir; aktif sistem alarmı değildir.</p>
          </article>
        </div>
      </section>

      <InfoNote>
        Geçmiş İş Emri Aktivitesi current toplamlarla birleştirilmez ve İş Emirleri ekranında ayrı dataset olarak sunulur.
        Yüksek asset aktivitesi, asset sağlığı veya arıza olasılığı anlamına gelmez.
      </InfoNote>
    </div>
  )
}
