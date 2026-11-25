import { ArrowRight } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { setPassword } from '../../api/auth'
import { ApiError } from '../../api/client'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { AuthLayout } from '../../layouts/AuthLayout'
import { NewPasswordFields } from './NewPasswordFields'
import { validateNewPassword, type NewPasswordErrors } from './passwordRules'

/** Davet bağlantısıyla hesap etkinleştirme ve ilk şifre (Stitch "Hesap etkinleştirme"). */
export function SetPasswordPage() {
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
      <AuthLayout title="Davet bağlantısı eksik">
        <Alert tone="warning">
          E-postanızdaki bağlantıyı açın. Bağlantıda şifre belirleme kodu olmalıdır. Bağlantının süresi dolduysa yöneticinizden yeni davet isteyin.
        </Alert>
        <Link to="/login">Giriş sayfasına dön</Link>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout title="Hesabınız etkinleştirildi" description="Şifreniz kaydedildi. Artık bu şifreyle giriş yapabilirsiniz.">
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
      await setPassword(token, password)
      setDone(true)
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : 'Şifre belirlenemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout
      title="Hesabınızı etkinleştirin"
      description="Davetiniz için bir şifre belirleyin. Bundan sonra bu şifreyle giriş yapacaksınız."
      footer={<p>Davet bağlantısı tek kullanımlıktır ve 72 saat geçerlidir.</p>}
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
          {pending ? 'Kaydediliyor…' : 'Hesabı etkinleştir'}
        </Button>
      </form>
    </AuthLayout>
  )
}
