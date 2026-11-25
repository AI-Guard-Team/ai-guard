import { ArrowRight } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { resetPassword } from '../../api/auth'
import { ApiError } from '../../api/client'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { AuthLayout } from '../../layouts/AuthLayout'
import { NewPasswordFields } from './NewPasswordFields'
import { validateNewPassword, type NewPasswordErrors } from './passwordRules'

/** E-postadaki bağlantıyla yeni şifre belirleme (MIMARI-KARARLAR K-33). */
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token')?.trim() ?? ''
  const [password, setPasswordValue] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<NewPasswordErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [done, setDone] = useState(false)

  if (!token) {
    return (
      <AuthLayout kicker="Şifre yenileme" title="Bağlantı eksik">
        <Alert tone="warning">Bu sayfa e-postadaki sıfırlama bağlantısıyla açılır. Bağlantının süresi dolduysa yenisini isteyin.</Alert>
        <Link to="/forgot-password">Yeni bağlantı iste</Link>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout kicker="Şifre yenileme"
        title="Şifreniz yenilendi"
        description="Güvenliğiniz için açık olan bütün oturumlarınız kapatıldı. Yeni şifrenizle giriş yapın."
      >
        <Link className="button button-primary button-lg button-block" to="/login">
          Giriş sayfasına git
          <ArrowRight size={18} aria-hidden="true" />
        </Link>
      </AuthLayout>
    )
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const next = validateNewPassword(password, confirm)
    setErrors(next)
    setFormError(null)
    if (next.password || next.confirm) return

    setPending(true)
    try {
      await resetPassword(token, password)
      setDone(true)
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : 'Şifre yenilenemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout kicker="Şifre yenileme"
      title="Yeni şifre belirleyin"
      description="Hesabınız için yeni bir şifre seçin."
      footer={<Link to="/forgot-password">Bağlantının süresi mi doldu? Yenisini isteyin</Link>}
    >
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <NewPasswordFields
          password={password}
          confirm={confirm}
          errors={errors}
          onPasswordChange={setPasswordValue}
          onConfirmChange={setConfirm}
        />
        <Button type="submit" size="lg" block loading={pending}>
          {pending ? 'Kaydediliyor…' : 'Şifreyi yenile'}
        </Button>
      </form>
    </AuthLayout>
  )
}
