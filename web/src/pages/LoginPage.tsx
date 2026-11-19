import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '../api/client'
import { login } from '../api/auth'
import { decodeAccessToken } from '../auth/jwt'
import { ROLE_HOME } from '../auth/roles'
import { useAuth } from '../auth/useAuth'
import { AuthCard, FormAlert } from '../components/AuthCard'
import { Button } from '../components/Button'
import { Input } from '../components/Input'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function requestedPath(state: unknown): string | null {
  if (!state || typeof state !== 'object' || !('from' in state)) return null
  const from = (state as { from?: unknown }).from
  // Yalnızca uygulama içi adres kabul edilir; dışarıya yönlendirme olmasın.
  if (typeof from !== 'string' || !from.startsWith('/') || from.startsWith('//') || from.startsWith('/login')) return null
  return from
}

export function LoginPage() {
  const { user, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({})
  const [formError, setFormError] = useState<string | null>(null)
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
    setFormError(null)
    if (next.email || next.password) return

    setPending(true)
    try {
      const tokens = await login(email.trim(), password)
      const session = decodeAccessToken(tokens.accessToken)
      if (!session) {
        setFormError('Giriş yanıtı okunamadı. Lütfen tekrar deneyin.')
        return
      }
      signIn(tokens)
      navigate(requestedPath(location.state) ?? ROLE_HOME[session.role], { replace: true })
    } catch (error) {
      // Girişte oturum yoktur; 401 burada "süre doldu" değil, hatalı bilgidir.
      if (error instanceof ApiError && error.status === 401) {
        setFormError('E-posta veya şifre hatalı.')
      } else if (error instanceof ApiError) {
        setFormError(error.message)
      } else {
        setFormError('Giriş yapılamadı. Lütfen tekrar deneyin.')
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard title="Giriş" lead="Hesabınızla sınav platformuna giriş yapın.">
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <Input
          label="E-posta"
          type="email"
          autoComplete="username"
          value={email}
          error={errors.email}
          onChange={(event) => setEmail(event.target.value)}
        />
        <Input
          label="Şifre"
          type="password"
          autoComplete="current-password"
          value={password}
          error={errors.password}
          onChange={(event) => setPassword(event.target.value)}
        />
        <Button type="submit" className="button-block" disabled={pending}>
          {pending ? 'Giriş yapılıyor…' : 'Giriş'}
        </Button>
      </form>
    </AuthCard>
  )
}
