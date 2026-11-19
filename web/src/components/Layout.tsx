import { NavLink, Outlet, useNavigate } from 'react-router'
import { ROLE_LABEL, ROLE_MENU } from '../auth/roles'
import { useAuth } from '../auth/useAuth'

export function Layout() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()

  if (!user) return null

  const handleSignOut = () => {
    signOut()
    navigate('/login', { replace: true })
  }

  return (
    <div className="layout">
      <header className="layout-header">
        <span className="layout-brand">AI-Guard</span>
        <nav className="layout-nav" aria-label="Ana menü">
          {ROLE_MENU[user.role].map((item) => (
            <NavLink key={item.to} to={item.to}>
              {item.label}
            </NavLink>
          ))}
          <NavLink to="/profile">Profil</NavLink>
        </nav>
        <div className="layout-user">
          <span>
            {user.name ?? user.email ?? 'Kullanıcı'} · {ROLE_LABEL[user.role]}
          </span>
          <button type="button" onClick={handleSignOut}>
            Çıkış
          </button>
        </div>
      </header>
      <main className="layout-main">
        <Outlet />
      </main>
    </div>
  )
}
