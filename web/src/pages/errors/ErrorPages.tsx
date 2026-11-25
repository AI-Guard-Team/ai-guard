import { Clock3, House, LogIn, SearchX, ShieldX } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router'
import { ROLE_HOME } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { Button } from '../../components/Button'
import { StatusScreen } from './StatusScreen'

function HomeLink() {
  const { user } = useAuth()
  return (
    <Link className="button button-primary button-md" to={user ? ROLE_HOME[user.role] : '/login'}>
      <House size={18} aria-hidden="true" />
      {user ? 'Ana sayfaya dön' : 'Giriş sayfasına git'}
    </Link>
  )
}

export function NotFoundPage() {
  return (
    <StatusScreen icon={SearchX} code="404" title="Sayfa bulunamadı" actions={<HomeLink />}>
      <p>Aradığınız sayfa yok ya da taşınmış olabilir. Adresi kontrol edin veya ana sayfaya dönün.</p>
    </StatusScreen>
  )
}

/** Rolün göremeyeceği bir sayfa açıldığında kabuğun içinde gösterilir (RequireAuth). */
export function ForbiddenPage() {
  const navigate = useNavigate()
  return (
    <StatusScreen
      icon={ShieldX}
      code="403"
      title="Bu sayfaya erişim yetkiniz yok"
      standalone={false}
      tone="danger"
      actions={
        <>
          <HomeLink />
          <Button variant="secondary" onClick={() => navigate(-1)}>
            Geri dön
          </Button>
        </>
      }
    >
      <p>Bu bölüm başka bir role açık. Erişim gerektiğini düşünüyorsanız kurum yöneticinize başvurun.</p>
    </StatusScreen>
  )
}

function returnPath(state: unknown): string | undefined {
  if (!state || typeof state !== 'object' || !('from' in state)) return undefined
  const from = (state as { from?: unknown }).from
  return typeof from === 'string' ? from : undefined
}

/** Yenileme belirteci reddedilince (süre doldu, şifre değişti, hesap kapatıldı) gösterilir. */
export function SessionExpiredPage() {
  const location = useLocation()
  const from = returnPath(location.state)
  return (
    <StatusScreen
      icon={Clock3}
      title="Oturumunuzun süresi doldu"
      actions={
        <Link className="button button-primary button-md" to="/login" state={from ? { from } : undefined}>
          <LogIn size={18} aria-hidden="true" />
          Tekrar giriş yap
        </Link>
      }
    >
      <p>Güvenliğiniz için uzun süre işlem yapılmayan oturumlar kapatılır. Giriş yaptıktan sonra kaldığınız sayfaya dönersiniz.</p>
      <p>Sınavdaysanız süre sunucuda işlemeye devam eder; hemen giriş yapıp kaldığınız yerden sürdürün.</p>
    </StatusScreen>
  )
}
