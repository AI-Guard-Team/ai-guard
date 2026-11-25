import { useState, type FormEvent } from 'react'
import { changePassword, login } from '../../api/auth'
import { ApiError } from '../../api/client'
import { ROLE_LABEL } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { PasswordInput } from '../../components/Input'
import { PageHeader } from '../../components/PageHeader'
import { Panel } from '../../components/Panel'
import { useToast } from '../../components/useToast'
import { NewPasswordFields } from '../auth/NewPasswordFields'
import { validateNewPassword, type NewPasswordErrors } from '../auth/passwordRules'
import { ProfileTabs } from './ProfileTabs'
import './ProfilePage.css'

function missing(value: string | undefined): string {
  return value?.trim() ? value : 'Kayıtlı değil'
}

/** Profil ve şifre değiştirme (Stitch ekran 5, GAB 3.4.5). Ad ve e-postayı yalnızca yönetici değiştirebilir. */
export function ProfilePage() {
  const { user, signIn } = useAuth()
  const showToast = useToast()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [currentError, setCurrentError] = useState<string>()
  const [errors, setErrors] = useState<NewPasswordErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  if (!user) return null

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const next = validateNewPassword(newPassword, confirm)
    if (!next.password && newPassword === currentPassword) next.password = 'Yeni şifre mevcut şifreyle aynı olamaz.'
    const current = currentPassword ? undefined : 'Mevcut şifre zorunludur.'
    setErrors(next)
    setCurrentError(current)
    setFormError(null)
    if (current || next.password || next.confirm) return

    setPending(true)
    try {
      await changePassword(currentPassword, newPassword)
      // Sunucu bütün yenileme belirteçlerini iptal ediyor (K-20). Bu sekme 15 dk sonra düşmesin diye
      // yeni şifreyle sessizce yeniden giriş yapılır; olmazsa oturum süresi dolunca yeniden giriş istenir.
      if (user.email) {
        try {
          signIn(await login(user.email, newPassword))
        } catch {
          // Şifre değişti; yeniden giriş yapılamaması değişikliği geçersiz kılmaz.
        }
      }
      setCurrentPassword('')
      setNewPassword('')
      setConfirm('')
      showToast('Şifreniz güncellendi. Diğer cihazlardaki oturumlarınız kapatıldı.', 'success')
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : 'Şifre güncellenemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <>
      <PageHeader title="Profil" description="Hesap bilgileriniz ve şifreniz." />
      <ProfileTabs />

      <div className="profile-grid">
        <Panel title="Hesap bilgileri">
          <dl className="profile-facts">
            <div>
              <dt>Ad soyad</dt>
              <dd>{missing(user.name)}</dd>
            </div>
            <div>
              <dt>E-posta</dt>
              <dd>{missing(user.email)}</dd>
            </div>
            <div>
              <dt>Rol</dt>
              <dd>{ROLE_LABEL[user.role]}</dd>
            </div>
          </dl>
          <Alert tone="info" live={false}>
            Ad, e-posta ve rol bilgilerinizi yalnızca kurum yöneticisi değiştirebilir.
          </Alert>
        </Panel>

        <Panel title="Şifre değiştir" description="Değişiklikten sonra diğer cihazlardaki oturumlarınız kapatılır.">
          <form onSubmit={handleSubmit} noValidate>
            <FormAlert message={formError} />
            <PasswordInput
              label="Mevcut şifre"
              autoComplete="current-password"
              value={currentPassword}
              error={currentError}
              onChange={(event) => setCurrentPassword(event.target.value)}
            />
            <NewPasswordFields
              password={newPassword}
              confirm={confirm}
              errors={errors}
              onPasswordChange={setNewPassword}
              onConfirmChange={setConfirm}
            />
            <Button type="submit" loading={pending}>
              {pending ? 'Kaydediliyor…' : 'Şifreyi güncelle'}
            </Button>
          </form>
        </Panel>
      </div>
    </>
  )
}
