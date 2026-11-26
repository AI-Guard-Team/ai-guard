import { ArrowRight } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { setPassword } from '../../api/auth'
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
    missingTitle: 'Davet bağlantısı eksik',
    missingText: 'E-postanızdaki bağlantıyı açın. Bağlantıda şifre belirleme kodu olmalıdır. Bağlantının süresi dolduysa yöneticinizden yeni davet isteyin.',
    doneTitle: 'Hesabınız etkinleştirildi',
    doneText: 'Şifreniz kaydedildi. Artık bu şifreyle giriş yapabilirsiniz.',
    failed: 'Şifre belirlenemedi. Lütfen tekrar deneyin.',
    invalidLink: 'Davet bağlantısı geçersiz ya da süresi dolmuş. Yöneticinizden yeni davet isteyin.',
    title: 'Hesabınızı etkinleştirin',
    description: 'Davetiniz için bir şifre belirleyin. Bundan sonra bu şifreyle giriş yapacaksınız.',
    footer: 'Davet bağlantısı tek kullanımlıktır ve 72 saat geçerlidir.',
    submit: 'Hesabı etkinleştir',
  },
  en: {
    missingTitle: 'Invitation link incomplete',
    missingText: 'Open the link in your email. It must contain a password setup code. If the link has expired, ask your administrator for a new invitation.',
    doneTitle: 'Your account is active',
    doneText: 'Your password has been saved. You can now sign in with it.',
    failed: 'The password could not be set. Please try again.',
    invalidLink: 'The invitation link is invalid or has expired. Ask your administrator for a new invitation.',
    title: 'Activate your account',
    description: 'Choose a password for your invitation. You will sign in with it from now on.',
    footer: 'The invitation link can be used once and is valid for 72 hours.',
    submit: 'Activate account',
  },
})

/** Davet bağlantısıyla hesap etkinleştirme ve ilk şifre (Stitch "Hesap etkinleştirme"). */
export function SetPasswordPage() {
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
        <Link to="/login">{copy.backToLogin}</Link>
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
      await setPassword(token, password)
      setDone(true)
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && !error.title) setFormError(m.invalidLink)
      else setFormError(error instanceof ApiError ? error.message : m.failed)
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout title={m.title} description={m.description} footer={<p>{m.footer}</p>}>
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
