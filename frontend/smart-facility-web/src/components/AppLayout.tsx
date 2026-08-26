import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'

const navigation = [
  { to: '/', label: 'Genel Bakış' },
  { to: '/assets', label: 'Varlıklar' },
  { to: '/work-orders', label: 'İş Emirleri' },
  { to: '/inspection-priority', label: 'İnceleme Önceliği' },
  { to: '/scada', label: 'SCADA' },
  { to: '/data-quality', label: 'Veri Kalitesi' },
]

const titles: Record<string, string> = {
  '/': 'Genel Bakış',
  '/assets': 'Varlık Analitiği',
  '/work-orders': 'İş Emri Analitiği',
  '/inspection-priority': 'İnceleme Önceliği',
  '/scada': 'SCADA Analitiği',
  '/data-quality': 'Veri Kalitesi',
}

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const location = useLocation()

  return (
    <div className="app-shell">
      <aside className={`sidebar${sidebarOpen ? ' sidebar--open' : ''}`} aria-label="Ana navigasyon">
        <div className="brand">
          <strong className="brand__name">GÜRSAN</strong>
          <span className="brand__company">TEKNİK HİZMETLER A.Ş.</span>
          <span className="brand__product">Bakım &amp; Güvenilirlik</span>
        </div>
        <nav className="sidebar-nav">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              onClick={() => setSidebarOpen(false)}
              className={({ isActive }) => `sidebar-link${isActive ? ' sidebar-link--active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-footer">
          <span className="sidebar-footer__accent" aria-hidden="true" />
          Teknik operasyon platformu
        </div>
      </aside>

      {sidebarOpen ? (
        <button
          className="sidebar-backdrop"
          type="button"
          aria-label="Menüyü kapat"
          onClick={() => setSidebarOpen(false)}
        />
      ) : null}

      <div className="app-body">
        <header className="topbar">
          <button
            className="menu-button"
            type="button"
            aria-label="Menüyü aç"
            aria-expanded={sidebarOpen}
            onClick={() => setSidebarOpen((current) => !current)}
          >
            <span />
            <span />
            <span />
          </button>
          <div className="topbar__title">
            <span className="topbar__context">Gürsan Teknik Hizmetler A.Ş.</span>
            <strong>{titles[location.pathname] ?? 'Analytics'}</strong>
          </div>
          <div className="topbar__status">
            <span className="topbar__status-label">Teknik operasyon</span>
            <strong>Karar destek görünümü</strong>
          </div>
        </header>
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
