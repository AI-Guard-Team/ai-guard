import { Navigate, Outlet, useLocation } from 'react-router'
import { ForbiddenPage } from '../pages/errors/ErrorPages'
import { ROLE_HOME, type Role } from './roles'
import { tokenStore } from './tokenStore'
import { useAuth } from './useAuth'

export function RequireAuth({ roles }: { roles?: Role[] }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) {
    // Girişten sonra kullanıcıyı açmak istediği sayfaya geri götürebilmek için adres saklanır.
    const from = location.pathname + location.search
    const target = tokenStore.endReason() === 'expired' ? '/session-expired' : '/login'
    return <Navigate to={target} replace state={{ from }} />
  }

  // Yönlendirmek yerine 403 gösterilir: kullanıcı neden o sayfayı göremediğini bilsin (Stitch ekran 6).
  if (roles && !roles.includes(user.role)) return <ForbiddenPage />

  return <Outlet />
}

export function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? ROLE_HOME[user.role] : '/login'} replace />
}
