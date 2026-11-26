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
import { getLang } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AuthLayout } from '../../layouts/AuthLayout'
import { authCopy } from './authCopy'
import { MAX_EMAIL_LENGTH, MAX_PASSWORD_LENGTH, validateEmail, validateLoginPassword } from './passwordRules'

const messages = defineMessages({
  tr: {
    title: 'Giriş yap',
    description: 'Kurum hesabınızla oturum açın.',
    footer: 'Hesabınızı kurum yöneticisi açar. Davet e-postanız yoksa yöneticinize başvurun.',
    unreadable: 'Giriş yanıtı okunamadı. Lütfen tekrar deneyin.',
    wrong: 'E-posta veya şifre hatalı.',
    locked: 'Hesap geçici olarak kilitlendi',
    lockedText: '5 hatalı denemeden sonra hesap 15 dakika kilitlenir. Biraz bekleyip tekrar deneyin ya da şifrenizi sıfırlayın.',
    failed: 'Giriş yapılamadı. Lütfen tekrar deneyin.',
    forgot: 'Şifremi unuttum',
    signingIn: 'Giriş yapılıyor…',
  },
  en: {
    title: 'Sign in',
    description: 'Sign in with your institution account.',
    footer: 'Accounts are opened by your institution’s administrator. If you have no invitation email, contact your administrator.',
    unreadable: 'The sign-in response could not be read. Please try again.',
    wrong: 'Incorrect email or password.',
    locked: 'Account temporarily locked',
    lockedText: 'After 5 failed attempts the account is locked for 15 minutes. Wait a little and try again, or reset your password.',
    failed: 'Sign-in failed. Please try again.',
    forgot: 'Forgot password?',
    signingIn: 'Signing in…',
  },
})

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
  const m = useMessages(messages)
  const copy = useMessages(authCopy)
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
    if (pending) return
    const next = { email: validateEmail(email), password: validateLoginPassword(password) }
    setErrors(next)
    setMessage(null)
    if (next.email || next.password) return

    setPending(true)
    try {
      const tokens = await login(email.trim(), password)
      const session = decodeAccessToken(tokens.accessToken)
      if (!session) {
        setMessage({ tone: 'danger', text: m.unreadable })
        return
      }
      signIn(tokens)
      navigate(requestedPath(location.state) ?? ROLE_HOME[session.role], { replace: true })
    } catch (error) {
      // Girişte oturum yoktur; 401 burada "süre doldu" değil, hatalı bilgidir.
      if (error instanceof ApiError && error.status === 401) {
        setMessage({ tone: 'danger', text: m.wrong })
      } else if (error instanceof ApiError && error.status === 429) {
        // Sunucu kalan dakikayı Türkçe "detail" içinde veriyor (K-20: 5 hatalı denemede 15 dk); İngilizcede genel açıklama.
        setMessage({ tone: 'warning', title: m.locked, text: getLang() === 'tr' ? error.message : m.lockedText })
      } else if (error instanceof ApiError) {
        setMessage({ tone: 'danger', text: error.message })
      } else {
        setMessage({ tone: 'danger', text: m.failed })
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout title={m.title} description={m.description} footer={<p>{m.footer}</p>}>
      <form onSubmit={handleSubmit} noValidate>
        {message ? (
          <Alert tone={message.tone} title={message.title}>
            {message.text}
          </Alert>
        ) : null}
        <Input
          label={copy.email}
          type="email"
          inputMode="email"
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          maxLength={MAX_EMAIL_LENGTH}
          placeholder={copy.emailPlaceholder}
          icon={<Mail size={18} />}
          value={email}
          error={errors.email}
          onChange={(event) => {
            setEmail(event.target.value)
            // Gösterilen hata düzeltildikçe kalkar; yazarken yeni hata çıkmaz.
            if (errors.email) setErrors((current) => ({ ...current, email: validateEmail(event.target.value) }))
          }}
          onBlur={() => {
            if (email) setErrors((current) => ({ ...current, email: validateEmail(email) }))
          }}
        />
        <PasswordInput
          label={copy.password}
          autoComplete="current-password"
          maxLength={MAX_PASSWORD_LENGTH}
          value={password}
          error={errors.password}
          onChange={(event) => {
            setPassword(event.target.value)
            if (errors.password) setErrors((current) => ({ ...current, password: validateLoginPassword(event.target.value) }))
          }}
        />
        <div className="auth-row">
          <Link to="/forgot-password">{m.forgot}</Link>
        </div>
        <Button type="submit" size="lg" block loading={pending} iconEnd={pending ? undefined : <ArrowRight size={18} aria-hidden="true" />}>
          {pending ? m.signingIn : m.title}
        </Button>
      </form>
    </AuthLayout>
  )
}
