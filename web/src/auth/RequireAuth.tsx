import { Navigate, Outlet, useLocation } from 'react-router'
import { ROLE_HOME, type Role } from './roles'
import { useAuth } from './useAuth'

export function RequireAuth({ roles }: { roles?: Role[] }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) {
    // Girişten sonra kullanıcıyı açmak istediği sayfaya geri götürebilmek için adres saklanır.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  if (roles && !roles.includes(user.role)) {
    return <Navigate to={ROLE_HOME[user.role]} replace />
  }

  return <Outlet />
}

export function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? ROLE_HOME[user.role] : '/login'} replace />
}
