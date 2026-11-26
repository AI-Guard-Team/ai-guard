import { LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { changePassword, login } from '../../api/auth'
import { ApiError } from '../../api/client'
import { ROLE_LABEL } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { FormAlert } from '../../components/Alert'
import { IconTile } from '../../components/IconTile'
import { Button } from '../../components/Button'
import { PasswordInput } from '../../components/Input'
import { Panel } from '../../components/Panel'
import { useToast } from '../../components/useToast'
import { defineMessages, useMessages } from '../../i18n/messages'
import { authCopy } from '../auth/authCopy'
import { NewPasswordFields } from '../auth/NewPasswordFields'
import { MAX_PASSWORD_LENGTH, validateNewPassword, type NewPasswordErrors } from '../auth/passwordRules'
import { ProfileHero } from './ProfileHero'
import { ProfileTabs } from './ProfileTabs'
import './ProfilePage.css'

const messages = defineMessages({
  tr: {
    missing: 'Kayıtlı değil',
    same: 'Yeni şifre mevcut şifreyle aynı olamaz.',
    currentRequired: 'Mevcut şifre zorunludur.',
    wrongCurrent: 'Mevcut şifre hatalı.',
    updated: 'Şifreniz güncellendi. Diğer cihazlardaki oturumlarınız kapatıldı.',
    failed: 'Şifre güncellenemedi. Lütfen tekrar deneyin.',
    account: 'Hesap bilgileri',
    name: 'Ad soyad',
    email: 'E-posta',
    role: 'Rol',
    note: 'Ad, e-posta ve rolü yalnızca kurum yöneticisi değiştirebilir.',
    change: 'Şifre değiştir',
    changeText: 'Değişiklikten sonra diğer cihazlardaki oturumlarınız kapatılır.',
    current: 'Mevcut şifre',
    submit: 'Şifreyi güncelle',
  },
  en: {
    missing: 'Not on record',
    same: 'The new password cannot be the same as the current one.',
    currentRequired: 'Current password is required.',
    wrongCurrent: 'The current password is incorrect.',
    updated: 'Your password has been updated. Your sessions on other devices were signed out.',
    failed: 'The password could not be updated. Please try again.',
    account: 'Account details',
    name: 'Full name',
    email: 'Email',
    role: 'Role',
    note: 'Only your institution’s administrator can change your name, email and role.',
    change: 'Change password',
    changeText: 'After the change, your sessions on other devices are signed out.',
    current: 'Current password',
    submit: 'Update password',
  },
})

/** Profil ve şifre değiştirme (Stitch ekran 5, GAB 3.4.5). Ad ve e-postayı yalnızca yönetici değiştirebilir. */
export function ProfilePage() {
  const m = useMessages(messages)
  const copy = useMessages(authCopy)
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
    if (pending) return
    const next = validateNewPassword(newPassword, confirm)
    if (!next.password && newPassword === currentPassword) next.password = m.same
    const current = currentPassword ? undefined : m.currentRequired
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
      showToast(m.updated, 'success')
    } catch (error) {
      // Sunucu mevcut şifre yanlışsa 400 veriyor; Türkçe neden yoksa alanın altında söylenir.
      if (error instanceof ApiError && error.status === 400 && !error.title) setCurrentError(m.wrongCurrent)
      else setFormError(error instanceof ApiError ? error.message : m.failed)
    } finally {
      setPending(false)
    }
  }

  const missing = (value: string | undefined) => (value?.trim() ? value : m.missing)

  return (
    <>
      <ProfileHero />
      <ProfileTabs />

      <div className="profile-grid">
        <Panel title={m.account}>
          <ul className="profile-rows">
            <li>
              <IconTile icon={UserRound} tone="indigo" size="sm" />
              <span>
                <span className="profile-row-label">{m.name}</span>
                <strong>{missing(user.name)}</strong>
              </span>
            </li>
            <li>
              <IconTile icon={Mail} tone="sky" size="sm" />
              <span>
                <span className="profile-row-label">{m.email}</span>
                <strong>{missing(user.email)}</strong>
              </span>
            </li>
            <li>
              <IconTile icon={ShieldCheck} tone="lilac" size="sm" />
              <span>
                <span className="profile-row-label">{m.role}</span>
                <strong>{ROLE_LABEL[user.role]}</strong>
              </span>
            </li>
          </ul>
          <p className="profile-note">{m.note}</p>
        </Panel>

        <Panel title={m.change} description={m.changeText} aside={<IconTile icon={LockKeyhole} tone="amber" size="sm" />}>
          <form onSubmit={handleSubmit} noValidate>
            <FormAlert message={formError} />
            <PasswordInput
              label={m.current}
              placeholder={copy.passwordPlaceholder}
              autoComplete="current-password"
              maxLength={MAX_PASSWORD_LENGTH}
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
              {pending ? copy.saving : m.submit}
            </Button>
          </form>
        </Panel>
      </div>
    </>
  )
}
