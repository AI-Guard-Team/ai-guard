import { ArrowRight } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { resetPassword } from '../../api/auth'
import { ApiError } from '../../api/client'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AuthLayout } from '../../layouts/AuthLayout'
import { authCopy } from './authCopy'
import { NewPasswordFields } from './NewPasswordFields'
import { validateNewPassword, type NewPasswordErrors } from './passwordRules'
import { useUrlToken } from './token'

const messages = defineMessages({
  tr: {
    missingTitle: 'Bağlantı eksik',
    missingText: 'Bu sayfa e-postadaki sıfırlama bağlantısıyla açılır. Bağlantının süresi dolduysa yenisini isteyin.',
    newLink: 'Yeni bağlantı iste',
    doneTitle: 'Şifreniz yenilendi',
    doneText: 'Güvenliğiniz için açık olan bütün oturumlarınız kapatıldı. Yeni şifrenizle giriş yapın.',
    failed: 'Şifre yenilenemedi. Lütfen tekrar deneyin.',
    invalidLink: 'Bağlantı geçersiz ya da süresi dolmuş. Yeni bir sıfırlama bağlantısı isteyin.',
    title: 'Yeni şifre belirleyin',
    description: 'Hesabınız için yeni bir şifre seçin.',
    expired: 'Bağlantının süresi mi doldu? Yenisini isteyin',
    submit: 'Şifreyi yenile',
  },
  en: {
    missingTitle: 'Link incomplete',
    missingText: 'This page opens from the reset link in your email. If the link has expired, request a new one.',
    newLink: 'Request a new link',
    doneTitle: 'Your password has been reset',
    doneText: 'For your security, all your open sessions were signed out. Sign in with your new password.',
    failed: 'The password could not be reset. Please try again.',
    invalidLink: 'The link is invalid or has expired. Request a new reset link.',
    title: 'Choose a new password',
    description: 'Pick a new password for your account.',
    expired: 'Link expired? Request a new one',
    submit: 'Reset password',
  },
})

/** E-postadaki bağlantıyla yeni şifre belirleme (MIMARI-KARARLAR K-33). */
export function ResetPasswordPage() {
  const m = useMessages(messages)
  const copy = useMessages(authCopy)
  const token = useUrlToken()
  const [password, setPasswordValue] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<NewPasswordErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [done, setDone] = useState(false)

  if (!token) {
    return (
      <AuthLayout title={m.missingTitle}>
        <Alert tone="warning">{m.missingText}</Alert>
        <Link to="/forgot-password">{m.newLink}</Link>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout title={m.doneTitle} description={m.doneText}>
        <Link className="button button-primary button-lg button-block" to="/login">
          {copy.goToLogin}
          <ArrowRight size={18} aria-hidden="true" />
        </Link>
      </AuthLayout>
    )
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (pending) return
    const next = validateNewPassword(password, confirm)
    setErrors(next)
    setFormError(null)
    if (next.password || next.confirm) return

    setPending(true)
    try {
      await resetPassword(token, password)
      setDone(true)
    } catch (error) {
      // 400: kural dışı şifre ya da geçersiz/süresi dolmuş bağlantı; sunucu nedeni Türkçe veriyor.
      if (error instanceof ApiError && error.status === 400 && !error.title) setFormError(m.invalidLink)
      else setFormError(error instanceof ApiError ? error.message : m.failed)
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout title={m.title} description={m.description} footer={<Link to="/forgot-password">{m.expired}</Link>}>
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <NewPasswordFields password={password} confirm={confirm} errors={errors} onPasswordChange={setPasswordValue} onConfirmChange={setConfirm} />
        <Button type="submit" size="lg" block loading={pending}>
          {pending ? copy.saving : m.submit}
        </Button>
      </form>
    </AuthLayout>
  )
}
