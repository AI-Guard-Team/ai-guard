import {
  BookOpenCheck,
  ClipboardCheck,
  ClipboardList,
  FileBarChart,
  LayoutDashboard,
  LogOut,
  Menu,
  ScrollText,
  Settings2,
  UserRound,
  UserSquare2,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react'
import { Suspense, useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { logout } from '../api/auth'
import { ROLE_LABEL, ROLE_MENU } from '../auth/roles'
import { useAuth } from '../auth/useAuth'
import { BrandMark } from '../components/BrandMark'
import { RouteFallback } from '../components/RouteFallback'
import './AppShell.css'

const ICONS: Record<string, LucideIcon> = {
  '/admin': LayoutDashboard,
  '/admin/exams': BookOpenCheck,
  '/admin/users': Users,
  '/admin/reports': FileBarChart,
  '/admin/settings': Settings2,
  '/admin/audit': ScrollText,
  '/review': ClipboardCheck,
  '/exams': ClipboardList,
  '/profile/photo': UserSquare2,
  '/profile': UserRound,
}

const PORTAL_LABEL = { Admin: 'Yönetim paneli', Reviewer: 'Değerlendirici paneli', Candidate: 'Aday portalı' } as const

function initials(name: string): string {
  // Yalnızca harfle başlayan sözcükler: "Örnek Aday 1" → "ÖA".
  const parts = name.trim().split(/\s+/).filter((part) => /^\p{L}/u.test(part))
  const letters = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : (parts[0] ?? '?').slice(0, 2)
  return letters.toLocaleUpperCase('tr-TR')
}

/**
 * Giriş yapılmış ekranların kabuğu (Stitch "Rol bazlı ana kabuk"). Menü rolden gelir (auth/roles.ts);
 * sayfada olmayan bir bölüm menüye eklenmez.
 */
export function AppShell() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [menuOpen, setMenuOpen] = useState(false)

  // Telefonda menüden bir sayfa seçilince menü kapanır.
  const [lastPath, setLastPath] = useState(location.pathname)
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname)
    setMenuOpen(false)
  }

  useEffect(() => {
    if (!menuOpen) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMenuOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [menuOpen])

  if (!user) return null

  const displayName = user.name ?? user.email ?? 'Kullanıcı'
  const items = [...ROLE_MENU[user.role], { to: '/profile', label: 'Profil', end: true }]

  const handleSignOut = () => {
    // Önce yönlendirilir; aksi hâlde RequireAuth mevcut sayfayı "girişten sonra dönülecek sayfa" olarak saklar.
    navigate('/login', { replace: true })
    void logout()
  }

  return (
    <div className="app-shell" data-menu-open={menuOpen || undefined}>
      <header className="app-topbar">
        <button
          type="button"
          className="app-menu-button"
          aria-expanded={menuOpen}
          aria-controls="app-sidebar"
          aria-label={menuOpen ? 'Menüyü kapat' : 'Menüyü aç'}
          onClick={() => setMenuOpen((open) => !open)}
        >
          {menuOpen ? <X size={22} aria-hidden="true" /> : <Menu size={22} aria-hidden="true" />}
        </button>
        <BrandMark tone="light" />
      </header>

      <aside id="app-sidebar" className="app-sidebar">
        <div className="app-sidebar-brand">
          <BrandMark subtitle={PORTAL_LABEL[user.role]} />
        </div>

        <nav className="app-nav" aria-label="Ana menü">
          {items.map((item) => {
            const Icon = ICONS[item.to] ?? LayoutDashboard
            return (
              <NavLink key={item.to} to={item.to} end={item.end} className="app-nav-link">
                <Icon size={20} aria-hidden="true" />
                <span>{item.label}</span>
              </NavLink>
            )
          })}
        </nav>

        <div className="app-user">
          <span className="app-user-avatar" aria-hidden="true">
            {initials(displayName)}
          </span>
          <span className="app-user-text">
            <span className="app-user-name">{displayName}</span>
            <span className="app-user-role">{ROLE_LABEL[user.role]}</span>
          </span>
          <button type="button" className="app-signout" onClick={handleSignOut} aria-label="Çıkış yap" title="Çıkış yap">
            <LogOut size={18} aria-hidden="true" />
          </button>
        </div>
      </aside>

      <button type="button" className="app-scrim" tabIndex={-1} aria-hidden="true" onClick={() => setMenuOpen(false)} />

      <main className="app-content" id="main">
        {/* Sayfa parçası inerken yalnızca içerik alanı bekler; yan menü yerinde kalır. */}
        <Suspense fallback={<RouteFallback />}>
          <Outlet />
        </Suspense>
      </main>
    </div>
  )
}
