import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ApiError } from '../api/client'
import { setPassword } from '../api/auth'
import { AuthCard, FormAlert } from '../components/AuthCard'
import { Button } from '../components/Button'
import { Input } from '../components/Input'

export function SetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token')?.trim() ?? ''
  const [password, setPasswordValue] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<{ password?: string; confirm?: string }>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [done, setDone] = useState(false)

  if (!token) {
    return (
      <AuthCard title="Şifre belirleme" lead="Davet bağlantısı eksik veya bozuk.">
        <p>E-postanızdaki bağlantıyı açın. Bağlantıda şifre belirleme kodu olmalıdır.</p>
        <Link to="/login">Giriş sayfasına dön</Link>
      </AuthCard>
    )
  }

  if (done) {
    return (
      <AuthCard title="Şifre belirlendi" lead="Artık bu şifreyle giriş yapabilirsiniz.">
        <Link to="/login">Giriş sayfasına git</Link>
      </AuthCard>
    )
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const next = {
      password: password ? undefined : 'Şifre zorunludur.',
      confirm: !confirm ? 'Şifre tekrarı zorunludur.' : confirm !== password ? 'Şifreler aynı olmalıdır.' : undefined,
    }
    setErrors(next)
    setFormError(null)
    if (next.password || next.confirm) return

    setPending(true)
    try {
      await setPassword(token, password)
      setDone(true)
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : 'Şifre belirlenemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard title="Şifre belirleme" lead="Davet bağlantınızdaki şifreyi belirleyin. Bundan sonra bu şifreyle giriş yapacaksınız.">
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <Input
          label="Yeni şifre"
          type="password"
          autoComplete="new-password"
          value={password}
          error={errors.password}
          onChange={(event) => setPasswordValue(event.target.value)}
        />
        <Input
          label="Yeni şifre (tekrar)"
          type="password"
          autoComplete="new-password"
          value={confirm}
          error={errors.confirm}
          onChange={(event) => setConfirm(event.target.value)}
        />
        <Button type="submit" className="button-block" disabled={pending}>
          {pending ? 'Kaydediliyor…' : 'Şifreyi kaydet'}
        </Button>
      </form>
    </AuthCard>
  )
}
