import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../api/client'
import { changePassword } from '../api/auth'
import { ROLE_LABEL } from '../auth/roles'
import { useAuth } from '../auth/useAuth'
import { Button } from '../components/Button'
import { FormAlert } from '../components/AuthCard'
import { Input } from '../components/Input'
import { useToast } from '../components/useToast'

function missing(value: string | undefined): string {
  return value?.trim() ? value : 'Belirteçte yok'
}

export function ProfilePage() {
  const { user } = useAuth()
  const showToast = useToast()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<{ current?: string; next?: string; confirm?: string }>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  if (!user) return null

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const next = {
      current: currentPassword ? undefined : 'Mevcut şifre zorunludur.',
      next: !newPassword ? 'Yeni şifre zorunludur.' : newPassword === currentPassword ? 'Yeni şifre mevcut şifreyle aynı olamaz.' : undefined,
      confirm: !confirm ? 'Şifre tekrarı zorunludur.' : confirm !== newPassword ? 'Şifreler aynı olmalıdır.' : undefined,
    }
    setErrors(next)
    setFormError(null)
    if (next.current || next.next || next.confirm) return

    setPending(true)
    try {
      await changePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      setConfirm('')
      showToast('Şifreniz güncellendi.', 'success')
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : 'Şifre güncellenemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="card">
      <h1>Profil</h1>
      <dl className="meta">
        <dt>Ad</dt>
        <dd>{missing(user.name)}</dd>
        <dt>E-posta</dt>
        <dd>{missing(user.email)}</dd>
        <dt>Rol</dt>
        <dd>{ROLE_LABEL[user.role]}</dd>
      </dl>

      {user.role === 'Candidate' ? (
        <p>
          <Link to="/profile/photo">Kayıt fotoğrafı</Link>
        </p>
      ) : null}

      <h2>Şifre değiştir</h2>
      <p className="auth-lead">Mevcut şifrenizi doğrulayıp yeni bir şifre belirleyin.</p>
      <form className="narrow-form" onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <Input
          label="Mevcut şifre"
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          error={errors.current}
          onChange={(event) => setCurrentPassword(event.target.value)}
        />
        <Input
          label="Yeni şifre"
          type="password"
          autoComplete="new-password"
          value={newPassword}
          error={errors.next}
          onChange={(event) => setNewPassword(event.target.value)}
        />
        <Input
          label="Yeni şifre (tekrar)"
          type="password"
          autoComplete="new-password"
          value={confirm}
          error={errors.confirm}
          onChange={(event) => setConfirm(event.target.value)}
        />
        <Button type="submit" disabled={pending}>
          {pending ? 'Kaydediliyor…' : 'Şifreyi güncelle'}
        </Button>
      </form>
    </section>
  )
}
