import { ArrowRight, Mail } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { login } from '../../api/auth'
import { ApiError } from '../../api/client'
import { decodeAccessToken } from '../../auth/jwt'
import { ROLE_HOME } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Input, PasswordInput } from '../../components/Input'
import { AuthLayout } from '../../layouts/AuthLayout'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function requestedPath(state: unknown): string | null {
  if (!state || typeof state !== 'object' || !('from' in state)) return null
  const from = (state as { from?: unknown }).from
  // Yalnızca uygulama içi adres kabul edilir; dışarıya yönlendirme olmasın.
  if (typeof from !== 'string' || !from.startsWith('/') || from.startsWith('//') || from.startsWith('/\\')) return null
  if (from.startsWith('/login') || from.startsWith('/session-expired')) return null
  return from
}

type FormMessage = { tone: 'danger' | 'warning'; title?: string; text: string }

export function LoginPage() {
  const { user, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({})
  const [message, setMessage] = useState<FormMessage | null>(null)
  const [pending, setPending] = useState(false)

  // signIn oturumu hemen açar. Bu satır, formun navigate çağrısından sonra da aynı adresi kullanır.
  if (user) return <Navigate to={requestedPath(location.state) ?? ROLE_HOME[user.role]} replace />

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const next = {
      email: !email.trim() ? 'E-posta zorunludur.' : EMAIL_PATTERN.test(email.trim()) ? undefined : 'Geçerli bir e-posta adresi girin.',
      password: password ? undefined : 'Şifre zorunludur.',
    }
    setErrors(next)
    setMessage(null)
    if (next.email || next.password) return

    setPending(true)
    try {
      const tokens = await login(email.trim(), password)
      const session = decodeAccessToken(tokens.accessToken)
      if (!session) {
        setMessage({ tone: 'danger', text: 'Giriş yanıtı okunamadı. Lütfen tekrar deneyin.' })
        return
      }
      signIn(tokens)
      navigate(requestedPath(location.state) ?? ROLE_HOME[session.role], { replace: true })
    } catch (error) {
      // Girişte oturum yoktur; 401 burada "süre doldu" değil, hatalı bilgidir.
      if (error instanceof ApiError && error.status === 401) {
        setMessage({ tone: 'danger', text: 'E-posta veya şifre hatalı.' })
      } else if (error instanceof ApiError && error.status === 429) {
        // Sunucu kalan dakikayı "detail" içinde veriyor (K-20: 5 hatalı denemede 15 dk).
        setMessage({ tone: 'warning', title: 'Hesap geçici olarak kilitlendi', text: error.message })
      } else if (error instanceof ApiError) {
        setMessage({ tone: 'danger', text: error.message })
      } else {
        setMessage({ tone: 'danger', text: 'Giriş yapılamadı. Lütfen tekrar deneyin.' })
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout kicker="AI-Guard"
      title="Giriş yap"
      footer={<p>Hesabınızı kurum yöneticisi açar. Davet e-postanız yoksa yöneticinize başvurun.</p>}
    >
      <form onSubmit={handleSubmit} noValidate>
        {message ? (
          <Alert tone={message.tone} title={message.title}>
            {message.text}
          </Alert>
        ) : null}
        <Input
          label="E-posta"
          type="email"
          inputMode="email"
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          placeholder="ad.soyad@kurum.edu.tr"
          icon={<Mail size={18} />}
          value={email}
          error={errors.email}
          onChange={(event) => setEmail(event.target.value)}
        />
        <PasswordInput
          label="Şifre"
          autoComplete="current-password"
          value={password}
          error={errors.password}
          onChange={(event) => setPassword(event.target.value)}
        />
        <div className="auth-row">
          <Link to="/forgot-password">Şifremi unuttum</Link>
        </div>
        <Button type="submit" size="lg" block loading={pending} iconEnd={pending ? undefined : <ArrowRight size={18} aria-hidden="true" />}>
          {pending ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </Button>
      </form>
    </AuthLayout>
  )
}
