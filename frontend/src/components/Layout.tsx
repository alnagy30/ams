import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth'
import { ROLE_LABELS } from '../lib'
import { applyTheme, getTheme, toggleTheme } from '../theme'
import type { Theme } from '../theme'

interface NavEntry {
  to: string
  label: string
  icon: string
  end?: boolean
  admin?: boolean
  perm?: string
}

interface NavSection {
  title: string
  items: NavEntry[]
}

const NAV: NavSection[] = [
  {
    title: 'عام',
    items: [{ to: '/', label: 'لوحة المعلومات', icon: 'dashboard', end: true }]
  },
  {
    title: 'الأصول والعمليات',
    items: [
      { to: '/assets', label: 'سجل الأصول', icon: 'box' },
      { to: '/employees', label: 'الموظفون', icon: 'users' },
      { to: '/custody', label: 'العهد والمقتنيات', icon: 'custody' },
      { to: '/transfers', label: 'الحركات والنقل', icon: 'transfer' }
    ]
  },
  {
    title: 'الدوريات والرقابة',
    items: [
      { to: '/inventory', label: 'الجرد الدوري', icon: 'clipboard', admin: true },
      { to: '/scrap', label: 'التخريد', icon: 'scrap', admin: true },
      { to: '/notifications', label: 'التنبيهات', icon: 'bell' }
    ]
  },
  {
    title: 'الإدارة',
    items: [
      { to: '/reference', label: 'البيانات المرجعية', icon: 'layers', admin: true },
      { to: '/reports', label: 'التقارير', icon: 'chart', admin: true },
      { to: '/import', label: 'الاستيراد من Excel', icon: 'upload', perm: 'Import' },
      { to: '/audit', label: 'سجل التدقيق', icon: 'shield', admin: true },
      { to: '/users', label: 'المستخدمون والأدوار', icon: 'userCog', admin: true },
      { to: '/backup', label: 'النسخ الاحتياطي', icon: 'backup', perm: 'Backup' }
    ]
  }
]

const TITLES: Record<string, string> = {
  '/': 'لوحة المعلومات',
  '/assets': 'سجل الأصول',
  '/employees': 'الموظفون',
  '/custody': 'العهد والمقتنيات',
  '/transfers': 'الحركات والنقل',
  '/inventory': 'الجرد الدوري',
  '/scrap': 'التخريد',
  '/notifications': 'التنبيهات',
  '/reference': 'البيانات المرجعية',
  '/reports': 'التقارير',
  '/import': 'الاستيراد من Excel',
  '/audit': 'سجل التدقيق',
  '/users': 'المستخدمون والأدوار',
  '/backup': 'النسخ الاحتياطي'
}

function titleFor(pathname: string): string {
  if (TITLES[pathname]) return TITLES[pathname]
  if (pathname.startsWith('/assets/new')) return 'أصل جديد'
  if (pathname.startsWith('/assets/')) return 'تفاصيل الأصل'
  return 'منظومة BH-AEP'
}

function visibleSections(sections: NavSection[], isAdmin: boolean, hasPerm: (p: string) => boolean): NavSection[] {
  return sections
    .map((s) => ({
      ...s,
      items: s.items.filter((i) => {
        if (i.admin) return isAdmin
        if (i.perm) return isAdmin || hasPerm(i.perm)
        return true
      })
    }))
    .filter((s) => s.items.length > 0)
}

export function Layout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [theme, setTheme] = useState<Theme>(getTheme())

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  const isAdmin = user?.role === 'Admin'
  const hasPerm = (p: string) => isAdmin || (user?.permissions ?? []).includes(p)
  const sections = visibleSections(NAV, isAdmin, hasPerm)

  const initials = (user?.username ?? '؟').slice(0, 2).toUpperCase()

  return (
    <div className="app-shell">
      <aside className={'sidebar' + (mobileOpen ? ' open' : '')}>
        <div className="brand">
          <div className="logo">أ</div>
          <div>
            <div className="title">منظومة الأصول الثابتة</div>
            <div className="subtitle">BH-AEP</div>
          </div>
        </div>
        {sections.map((sec) => (
          <div key={sec.title}>
            <div className="nav-section">{sec.title}</div>
            {sec.items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
                end={it.end}
                onClick={() => setMobileOpen(false)}
                className={({ isActive }) => 'nav-item' + (isActive ? ' active' : '')}
              >
                <span className="ico">{navIcon(it.icon)}</span>
                <span className="lbl">{it.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </aside>

      <div className="main">
        <div className="topbar">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.7rem' }}>
            <button className="menu-toggle" onClick={() => setMobileOpen((v) => !v)} aria-label="القائمة">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                <path d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
            <div className="page-title">{titleFor(location.pathname)}</div>
          </div>
          <div className="user-area">
            <button
              className="icon-btn"
              onClick={() => setTheme(toggleTheme(theme))}
              title={theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن'}
              aria-label="تبديل المظهر"
            >
              {theme === 'dark' ? <SunIcon /> : <MoonIcon />}
            </button>
            <div className="avatar">{initials}</div>
            <div>
              <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>{user?.username}</div>
              <div className="muted">{user ? ROLE_LABELS[user.role] ?? user.role : ''}</div>
            </div>
            <button className="btn btn-ghost btn-sm" onClick={() => logout()} title="تسجيل الخروج">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
                <path d="m15 17-5-5 5-5" />
                <path d="M20 12H10" />
              </svg>
              خروج
            </button>
          </div>
        </div>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function SunIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M19.07 4.93l-1.41 1.41M6.34 17.66l-1.41 1.41" />
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  )
}

function navIcon(name: string) {
  const common = {
    width: 24,
    height: 24,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const
  }
  switch (name) {
    case 'dashboard':
      return <svg {...common}><rect x="3" y="3" width="7" height="9" rx="1.6" /><rect x="14" y="3" width="7" height="5" rx="1.6" /><rect x="14" y="12" width="7" height="9" rx="1.6" /><rect x="3" y="16" width="7" height="5" rx="1.6" /></svg>
    case 'box':
      return <svg {...common}><path d="M21 8 12 3 3 8v8l9 5 9-5z" /><path d="m3 8 9 5 9-5M12 13v8" /></svg>
    case 'users':
      return <svg {...common}><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13A4 4 0 0 1 16 11" /></svg>
    case 'custody':
      return <svg {...common}><path d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2" /><rect x="9" y="3" width="6" height="4" rx="1" /><path d="m9 14 2 2 4-4" /></svg>
    case 'transfer':
      return <svg {...common}><path d="M7 4 3 8l4 4" /><path d="M3 8h13a4 4 0 0 1 0 8h-1" /><path d="m17 20 4-4-4-4" /></svg>
    case 'clipboard':
      return <svg {...common}><rect x="8" y="2" width="8" height="4" rx="1.4" /><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" /><path d="m8 13 2 2 4-4" /></svg>
    case 'scrap':
      return <svg {...common}><path d="M3 6h18M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M10 11v6M14 11v6" /></svg>
    case 'bell':
      return <svg {...common}><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" /></svg>
    case 'layers':
      return <svg {...common}><path d="m12 2 9 5-9 5-9-5z" /><path d="m3 12 9 5 9-5M3 17l9 5 9-5" /></svg>
    case 'chart':
      return <svg {...common}><path d="M3 3v18h18" /><path d="m7 14 4-4 3 3 5-6" /></svg>
    case 'upload':
      return <svg {...common}><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><path d="m7 9 5-5 5 5M12 4v12" /></svg>
    case 'shield':
      return <svg {...common}><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /><path d="m9 12 2 2 4-4" /></svg>
    case 'userCog':
      return <svg {...common}><circle cx="9" cy="7" r="4" /><path d="M3 21v-2a4 4 0 0 1 4-4h4" /><circle cx="18" cy="15" r="3" /><path d="M18 12v1M18 17v1M15 15h1M20 15h1" /></svg>
    case 'backup':
      return <svg {...common}><path d="M21 12a9 9 0 1 1-3-6.7L21 8" /><path d="M21 3v5h-5" /></svg>
    default:
      return <svg {...common}><circle cx="12" cy="12" r="9" /></svg>
  }
}
