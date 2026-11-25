import { Navigate, Outlet, useLocation } from 'react-router'
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

  // Rolün açamadığı sayfa kullanıcıya hiç gösterilmez; kendi ana sayfasına gider (kullanıcı isteği, K-43).
  // Örn. yönetici sayfasında oturumu kapanıp aday olarak giren kişi yönetici adresine düşmez.
  if (roles && !roles.includes(user.role)) return <Navigate to={ROLE_HOME[user.role]} replace />

  return <Outlet />
}

export function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? ROLE_HOME[user.role] : '/login'} replace />
}
