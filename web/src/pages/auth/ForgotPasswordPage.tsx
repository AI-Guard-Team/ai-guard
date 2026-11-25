import { ArrowLeft, Mail } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { requestPasswordReset } from '../../api/auth'
import { ApiError } from '../../api/client'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'
import { AuthLayout } from '../../layouts/AuthLayout'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function BackToLogin() {
  return (
    <Link className="auth-back" to="/login">
      <ArrowLeft size={16} aria-hidden="true" />
      Giriş sayfasına dön
    </Link>
  )
}

/** Şifremi unuttum: bağlantı isteği (MIMARI-KARARLAR K-33). */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string>()
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)

  if (sentTo) {
    // Sunucu hesap olsun olmasın aynı yanıtı veriyor; metin de "gönderildi" değil "gönderilir" diyor.
    return (
      <AuthLayout title="E-postanızı kontrol edin" footer={<BackToLogin />}>
        <Alert tone="info">
          <strong>{sentTo}</strong> adresiyle kayıtlı bir hesap varsa şifre sıfırlama bağlantısı birkaç dakika içinde gelir. Bağlantı 30 dakika geçerlidir ve
          tek kullanımlıktır.
        </Alert>
        <p className="field-hint">E-posta gelmediyse gereksiz klasörüne bakın; birkaç dakika sonra yeniden isteyebilirsiniz.</p>
      </AuthLayout>
    )
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const value = email.trim()
    const next = !value ? 'E-posta zorunludur.' : EMAIL_PATTERN.test(value) ? undefined : 'Geçerli bir e-posta adresi girin.'
    setError(next)
    setFormError(null)
    if (next) return

    setPending(true)
    try {
      await requestPasswordReset(value)
      setSentTo(value)
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : 'İstek gönderilemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout
      title="Şifrenizi mi unuttunuz?"
      description="Kayıtlı e-posta adresinizi girin; şifrenizi yenilemeniz için bir bağlantı gönderelim."
      footer={<BackToLogin />}
    >
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <Input
          label="E-posta"
          type="email"
          inputMode="email"
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          placeholder="ad.soyad@kurum.edu.tr"
          icon={<Mail size={18} />}
          value={email}
          error={error}
          onChange={(event) => setEmail(event.target.value)}
        />
        <Button type="submit" size="lg" block loading={pending}>
          {pending ? 'Gönderiliyor…' : 'Sıfırlama bağlantısı gönder'}
        </Button>
      </form>
    </AuthLayout>
  )
}
