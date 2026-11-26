import { Clock3, House, LogIn, SearchX, ShieldX } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router'
import { ROLE_HOME } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { Button } from '../../components/Button'
import { defineMessages, useMessages } from '../../i18n/messages'
import { StatusScreen } from './StatusScreen'

const messages = defineMessages({
  tr: {
    home: 'Ana sayfaya dön',
    login: 'Giriş sayfasına git',
    notFound: 'Sayfa bulunamadı',
    notFoundText: 'Aradığınız sayfa yok ya da taşınmış olabilir. Adresi kontrol edin veya ana sayfaya dönün.',
    forbidden: 'Bu sayfaya erişim yetkiniz yok',
    forbiddenText: 'Bu bölüm başka bir role açık. Erişim gerektiğini düşünüyorsanız kurum yöneticinize başvurun.',
    back: 'Geri dön',
    expired: 'Oturumunuzun süresi doldu',
    signInAgain: 'Tekrar giriş yap',
    expiredText: 'Güvenliğiniz için uzun süre işlem yapılmayan oturumlar kapatılır. Giriş yaptıktan sonra kaldığınız sayfaya dönersiniz.',
    expiredExam: 'Sınavdaysanız süre sunucuda işlemeye devam eder; hemen giriş yapıp kaldığınız yerden sürdürün.',
  },
  en: {
    home: 'Back to home',
    login: 'Go to sign in',
    notFound: 'Page not found',
    notFoundText: 'The page you are looking for does not exist or may have moved. Check the address or go back to the home page.',
    forbidden: 'You do not have access to this page',
    forbiddenText: 'This section is open to another role. If you think you need access, contact your institution’s administrator.',
    back: 'Go back',
    expired: 'Your session has expired',
    signInAgain: 'Sign in again',
    expiredText: 'For your security, inactive sessions are closed. After signing in you return to the page you were on.',
    expiredExam: 'If you are in an exam, the timer keeps running on the server; sign in right away and continue where you left off.',
  },
})

function HomeLink() {
  const m = useMessages(messages)
  const { user } = useAuth()
  return (
    <Link className="button button-primary button-md" to={user ? ROLE_HOME[user.role] : '/login'}>
      <House size={18} aria-hidden="true" />
      {user ? m.home : m.login}
    </Link>
  )
}

export function NotFoundPage() {
  const m = useMessages(messages)
  return (
    <StatusScreen icon={SearchX} code="404" title={m.notFound} actions={<HomeLink />}>
      <p>{m.notFoundText}</p>
    </StatusScreen>
  )
}

/** Rolün göremeyeceği bir sayfa açıldığında kabuğun içinde gösterilir (RequireAuth). */
export function ForbiddenPage() {
  const m = useMessages(messages)
  const navigate = useNavigate()
  return (
    <StatusScreen
      icon={ShieldX}
      code="403"
      title={m.forbidden}
      standalone={false}
      tone="danger"
      actions={
        <>
          <HomeLink />
          <Button variant="secondary" onClick={() => navigate(-1)}>
            {m.back}
          </Button>
        </>
      }
    >
      <p>{m.forbiddenText}</p>
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
  const m = useMessages(messages)
  const location = useLocation()
  const from = returnPath(location.state)
  return (
    <StatusScreen
      icon={Clock3}
      title={m.expired}
      actions={
        <Link className="button button-primary button-md" to="/login" state={from ? { from } : undefined}>
          <LogIn size={18} aria-hidden="true" />
          {m.signInAgain}
        </Link>
      }
    >
      <p>{m.expiredText}</p>
      <p>{m.expiredExam}</p>
    </StatusScreen>
  )
}
