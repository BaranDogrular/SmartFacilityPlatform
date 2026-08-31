import { useState, type ReactNode } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import gursanLogo from '../assets/branding/gursan-logo-red-white.png'

type NavigationIcon = 'overview' | 'assets' | 'orders' | 'priority' | 'warning' | 'scada' | 'quality'

const navigation = [
  {
    label: 'Operasyon',
    items: [
      { to: '/', label: 'Genel Bakış', icon: 'overview' as const },
      { to: '/assets', label: 'Varlıklar', icon: 'assets' as const },
      { to: '/work-orders', label: 'İş Emirleri', icon: 'orders' as const },
    ],
  },
  {
    label: 'Karar destek',
    items: [
      { to: '/inspection-priority', label: 'İnceleme Önceliği', icon: 'priority' as const },
      { to: '/early-warning', label: 'Erken Uyarı', icon: 'warning' as const },
    ],
  },
  {
    label: 'Veri ve denetim',
    items: [
      { to: '/scada', label: 'SCADA', icon: 'scada' as const },
      { to: '/data-quality', label: 'Veri Kalitesi', icon: 'quality' as const },
    ],
  },
]

const pageDetails: Record<string, { title: string; context: string }> = {
  '/': { title: 'Genel Bakış', context: 'Operasyon merkezi' },
  '/assets': { title: 'Varlık Analitiği', context: 'Varlık portföyü' },
  '/work-orders': { title: 'İş Emri Analitiği', context: 'Canonical bakım kayıtları' },
  '/inspection-priority': { title: 'İnceleme Önceliği', context: 'Karar destek' },
  '/early-warning': { title: 'Erken Uyarı', context: 'Davranış sapması' },
  '/scada': { title: 'SCADA Analitiği', context: 'Operasyon verisi' },
  '/data-quality': { title: 'Veri Kalitesi', context: 'Import ve lineage denetimi' },
}

function NavigationGlyph({ name }: { name: NavigationIcon }) {
  const paths: Record<NavigationIcon, ReactNode> = {
    overview: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
    assets: <><path d="M12 3v6" /><path d="M6 21v-4a6 6 0 0 1 12 0v4" /><circle cx="12" cy="11" r="3" /></>,
    orders: <><path d="M7 3h10v4H7z" /><path d="M5 5v16h14V5" /><path d="M8 12h8M8 16h6" /></>,
    priority: <><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="3" /><path d="M12 2v3M22 12h-3M12 22v-3M2 12h3" /></>,
    warning: <><path d="M12 3 3 20h18L12 3Z" /><path d="M12 9v5" /><path d="M12 17h.01" /></>,
    scada: <><path d="M3 18h18" /><path d="M5 15l4-4 3 2 6-7" /><circle cx="18" cy="6" r="2" /></>,
    quality: <><path d="M12 3 5 6v5c0 4.7 2.9 8.2 7 10 4.1-1.8 7-5.3 7-10V6l-7-3Z" /><path d="m9 12 2 2 4-5" /></>,
  }

  return (
    <svg className="sidebar-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      {paths[name]}
    </svg>
  )
}

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const location = useLocation()
  const page = location.pathname.startsWith('/work-orders/')
    ? { title: 'Benzer Geçmiş Vakalar', context: 'Canonical vaka karşılaştırması' }
    : (pageDetails[location.pathname] ?? { title: 'Analytics', context: 'Teknik operasyon' })

  return (
    <div className="app-shell">
      <aside className={`sidebar${sidebarOpen ? ' sidebar--open' : ''}`} aria-label="Ana navigasyon">
        <div className="brand">
          <Link className="brand__home" to="/">
            <img className="brand__logo" src={gursanLogo} alt="Gürsan Teknik Hizmetler" />
          </Link>
          <span className="brand__product">Bakım &amp; Güvenilirlik</span>
        </div>
        <nav className="sidebar-nav">
          {navigation.map((group) => (
            <div className="sidebar-nav__group" key={group.label}>
              <span className="sidebar-nav__label">{group.label}</span>
              {group.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/'}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) => `sidebar-link${isActive ? ' sidebar-link--active' : ''}`}
                >
                  <NavigationGlyph name={item.icon} />
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="sidebar-footer">
          <span className="sidebar-footer__accent" aria-hidden="true" />
          <span><strong>SmartFacility V1.0</strong>Teknik operasyon platformu</span>
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
            <span className="topbar__context">{page.context}</span>
            <strong>{page.title}</strong>
          </div>
          <div className="topbar__status">
            <span className="topbar__status-label">Veri görünümü</span>
            <strong><span className="topbar__status-dot" aria-hidden="true" />Canonical operasyon verisi</strong>
          </div>
        </header>
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
