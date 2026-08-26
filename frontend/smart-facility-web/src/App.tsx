import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { LoadingState } from './components/DashboardUi'

const OverviewPage = lazy(async () => ({ default: (await import('./pages/OverviewPage')).OverviewPage }))
const AssetsPage = lazy(async () => ({ default: (await import('./pages/AssetsPage')).AssetsPage }))
const WorkOrdersPage = lazy(async () => ({ default: (await import('./pages/WorkOrdersPage')).WorkOrdersPage }))
const InspectionPriorityPage = lazy(async () => ({ default: (await import('./pages/InspectionPriorityPage')).InspectionPriorityPage }))
const EarlyWarningPage = lazy(async () => ({ default: (await import('./pages/EarlyWarningPage')).EarlyWarningPage }))
const ScadaPage = lazy(async () => ({ default: (await import('./pages/ScadaPage')).ScadaPage }))
const DataQualityPage = lazy(async () => ({ default: (await import('./pages/DataQualityPage')).DataQualityPage }))

function App() {
  return (
    <Suspense fallback={<LoadingState label="Dashboard ekranı yükleniyor" />}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<OverviewPage />} />
          <Route path="assets" element={<AssetsPage />} />
          <Route path="work-orders" element={<WorkOrdersPage />} />
          <Route path="inspection-priority" element={<InspectionPriorityPage />} />
          <Route path="early-warning" element={<EarlyWarningPage />} />
          <Route path="scada" element={<ScadaPage />} />
          <Route path="data-quality" element={<DataQualityPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </Suspense>
  )
}

export default App
