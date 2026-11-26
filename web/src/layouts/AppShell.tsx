import { ChevronsLeft, ChevronsRight, CircleHelp, LogOut, Menu, X } from 'lucide-react'
import { motion } from 'motion/react'
import { Suspense, useCallback, useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { logout } from '../api/auth'
import { ROLE_LABEL, ROLE_MENU, type Role } from '../auth/roles'
import { useAuth } from '../auth/useAuth'
import { BrandMark } from '../components/BrandMark'
import { IconTile } from '../components/IconTile'
import { RouteErrorBoundary } from '../components/RouteErrorBoundary'
import { RouteFallback } from '../components/RouteFallback'
import { LanguageToggle } from '../components/LanguageToggle'
import { ThemeToggle } from '../components/ThemeToggle'
import { getLocale } from '../i18n/lang'
import { defineMessages, useMessages } from '../i18n/messages'
import { OnboardingDialog } from './OnboardingDialog'
import { SECTION_ICON } from './sections'
import './AppShell.css'

const messages = defineMessages({
  tr: {
    portal: { Admin: 'Yönetim paneli', Reviewer: 'Değerlendirici paneli', Candidate: 'Aday portalı' } as Record<Role, string>,
    user: 'Kullanıcı',
    profile: 'Profil',
    openMenu: 'Menüyü aç',
    closeMenu: 'Menüyü kapat',
    mainMenu: 'Ana menü',
    menu: 'Menü',
    tour: 'Tanıtımı yeniden göster',
    expand: 'Menüyü genişlet',
    collapse: 'Menüyü daralt',
    signOut: 'Çıkış yap',
  },
  en: {
    portal: { Admin: 'Administration', Reviewer: 'Reviewer workspace', Candidate: 'Candidate portal' },
    user: 'User',
    profile: 'Profile',
    openMenu: 'Open menu',
    closeMenu: 'Close menu',
    mainMenu: 'Main menu',
    menu: 'Menu',
    tour: 'Show the tour again',
    expand: 'Expand menu',
    collapse: 'Collapse menu',
    signOut: 'Sign out',
  },
})

const onboardingKey = (role: Role) => `aiguard.onboarding.${role}.v2`
const COLLAPSE_KEY = 'aiguard.sidebar.collapsed'

function initials(name: string): string {
  // Yalnızca harfle başlayan sözcükler: "Örnek Aday 1" → "ÖA".
  const parts = name.trim().split(/\s+/).filter((part) => /^\p{L}/u.test(part))
  const letters = parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : (parts[0] ?? '?').slice(0, 2)
  return letters.toLocaleUpperCase(getLocale())
}

function stored(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function store(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Gizli sekmede depolama kapalı olabilir; tercih yalnızca bu sekmede geçerli.
  }
}

/**
 * Tanıtım her girişte bir kez açılır; "Bir daha gösterme" seçilirse bir daha açılmaz. Aynı oturumda kapatılınca
 * tekrar açılmaz. Testlerde açılmaz (sayfa testlerini örtmesin).
 */
function shouldShowOnboarding(role: Role): boolean {
  if (import.meta.env.MODE === 'test' || stored(onboardingKey(role)) !== null) return false
  try {
    return sessionStorage.getItem(onboardingKey(role)) === null
  } catch {
    return true
  }
}

/**
 * Giriş yapılmış ekranların kabuğu. Menü rolden gelir (auth/roles.ts). Masaüstünde menü daraltılabilir: daraltınca
 * yalnızca renkli simgeler kalır, adlar ipucunda. Telefonda menü çekmece olur.
 */
export function AppShell() {
  const m = useMessages(messages)
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [menuOpen, setMenuOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(() => stored(COLLAPSE_KEY) === '1')
  const [onboardingOpen, setOnboardingOpen] = useState(() => (user ? shouldShowOnboarding(user.role) : false))

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

  const role = user?.role
  const closeOnboarding = useCallback(
    (dontShowAgain: boolean) => {
      setOnboardingOpen(false)
      if (!role) return
      if (dontShowAgain) store(onboardingKey(role), new Date().toISOString())
      try {
        sessionStorage.setItem(onboardingKey(role), '1')
      } catch {
        // Depolama kapalıysa tanıtım bir sonraki sayfa açılışında yeniden görünebilir.
      }
    },
    [role],
  )

  const toggleCollapsed = () =>
    setCollapsed((value) => {
      store(COLLAPSE_KEY, value ? '0' : '1')
      return !value
    })

  if (!user) return null

  const displayName = user.name ?? user.email ?? m.user
  const items = [...ROLE_MENU[user.role], { to: '/profile', label: m.profile, end: true }]

  const handleSignOut = () => {
    // Önce yönlendirilir; aksi hâlde RequireAuth mevcut sayfayı "girişten sonra dönülecek sayfa" olarak saklar.
    navigate('/login', { replace: true })
    void logout()
  }

  return (
    <div className="app-shell" data-menu-open={menuOpen || undefined} data-collapsed={collapsed || undefined}>
      <header className="app-topbar">
        <button
          type="button"
          className="app-menu-button"
          aria-expanded={menuOpen}
          aria-controls="app-sidebar"
          aria-label={menuOpen ? m.closeMenu : m.openMenu}
          onClick={() => setMenuOpen((open) => !open)}
        >
          {menuOpen ? <X size={22} aria-hidden="true" /> : <Menu size={22} aria-hidden="true" />}
        </button>
        <BrandMark tone="light" />
      </header>

      <aside id="app-sidebar" className="app-sidebar">
        <div className="app-sidebar-brand">
          <BrandMark subtitle={m.portal[user.role]} />
        </div>

        <nav className="app-nav" aria-label={m.mainMenu}>
          <p className="app-nav-title">{m.menu}</p>
          {items.map((item) => {
            const section = SECTION_ICON[item.to] ?? SECTION_ICON['/overview']
            return (
              <NavLink key={item.to} to={item.to} end={item.end} className="app-nav-link" title={collapsed ? item.label : undefined}>
                {({ isActive }) => (
                  <>
                    {isActive ? (
                      <motion.span layoutId="app-nav-active" className="app-nav-active" transition={{ type: 'spring', stiffness: 520, damping: 42 }} />
                    ) : null}
                    <IconTile icon={section.icon} tone={section.tone} size="sm" />
                    <span className="app-nav-label">{item.label}</span>
                  </>
                )}
              </NavLink>
            )
          })}
        </nav>

        <div className="app-sidebar-foot">
          <ThemeToggle />
          <LanguageToggle />
          <button type="button" className="app-help" onClick={() => setOnboardingOpen(true)} title={collapsed ? m.tour : undefined}>
            <CircleHelp size={18} aria-hidden="true" />
            <span className="app-nav-label">{m.tour}</span>
          </button>
          <button type="button" className="app-help app-collapse" onClick={toggleCollapsed} aria-pressed={collapsed} title={collapsed ? m.expand : m.collapse}>
            {collapsed ? <ChevronsRight size={18} aria-hidden="true" /> : <ChevronsLeft size={18} aria-hidden="true" />}
            <span className="app-nav-label">{m.collapse}</span>
          </button>
          <div className="app-user">
            <span className="app-user-avatar" aria-hidden="true">
              {initials(displayName)}
            </span>
            <span className="app-user-text">
              <span className="app-user-name">{displayName}</span>
              <span className="app-user-role">{ROLE_LABEL[user.role]}</span>
            </span>
            <button type="button" className="app-signout" onClick={handleSignOut} aria-label={m.signOut} title={m.signOut}>
              <LogOut size={18} aria-hidden="true" />
            </button>
          </div>
        </div>
      </aside>

      <button type="button" className="app-scrim" tabIndex={-1} aria-hidden="true" onClick={() => setMenuOpen(false)} />

      <main className="app-content" id="main">
        {/* Sayfa parçası inerken yalnızca içerik alanı bekler; yan menü yerinde kalır. Sayfa değişince yumuşak giriş. */}
        <RouteErrorBoundary resetKey={location.pathname}>
          <Suspense fallback={<RouteFallback />}>
            <motion.div
              key={location.pathname}
              className="app-page"
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
            >
              <Outlet />
            </motion.div>
          </Suspense>
        </RouteErrorBoundary>
      </main>

      <OnboardingDialog role={user.role} open={onboardingOpen} onClose={closeOnboarding} />
    </div>
  )
}
