import { ArrowLeft, Mail } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { requestPasswordReset } from '../../api/auth'
import { ApiError } from '../../api/client'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AuthLayout } from '../../layouts/AuthLayout'
import { authCopy } from './authCopy'
import { MAX_EMAIL_LENGTH, validateEmail } from './passwordRules'

const messages = defineMessages({
  tr: {
    sentTitle: 'E-postanızı kontrol edin',
    sentText: (email: string) => (
      <>
        <strong>{email}</strong> adresiyle kayıtlı bir hesap varsa şifre sıfırlama bağlantısı birkaç dakika içinde gelir. Bağlantı 30 dakika geçerlidir
        ve tek kullanımlıktır.
      </>
    ),
    sentHint: 'E-posta gelmediyse gereksiz klasörüne bakın; birkaç dakika sonra yeniden isteyebilirsiniz.',
    another: 'Başka bir adres dene',
    title: 'Şifrenizi mi unuttunuz?',
    description: 'Kayıtlı e-posta adresinizi girin; şifrenizi yenilemeniz için bir bağlantı gönderelim.',
    failed: 'İstek gönderilemedi. Lütfen tekrar deneyin.',
    sending: 'Gönderiliyor…',
    send: 'Sıfırlama bağlantısı gönder',
  },
  en: {
    sentTitle: 'Check your email',
    sentText: (email: string) => (
      <>
        If an account is registered with <strong>{email}</strong>, a password reset link will arrive within a few minutes. The link is valid for 30
        minutes and can be used once.
      </>
    ),
    sentHint: 'If the email does not arrive, check your spam folder; you can request a new link in a few minutes.',
    another: 'Try another address',
    title: 'Forgot your password?',
    description: 'Enter your registered email address and we will send you a link to reset your password.',
    failed: 'The request could not be sent. Please try again.',
    sending: 'Sending…',
    send: 'Send reset link',
  },
})

function BackToLogin() {
  const copy = useMessages(authCopy)
  return (
    <Link className="auth-back" to="/login">
      <ArrowLeft size={16} aria-hidden="true" />
      {copy.backToLogin}
    </Link>
  )
}

/** Şifremi unuttum: bağlantı isteği (MIMARI-KARARLAR K-33). */
export function ForgotPasswordPage() {
  const m = useMessages(messages)
  const copy = useMessages(authCopy)
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string>()
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)

  if (sentTo) {
    // Sunucu hesap olsun olmasın aynı yanıtı veriyor; metin de "gönderildi" değil "gönderilir" diyor (e-posta sızmasın).
    return (
      <AuthLayout title={m.sentTitle} footer={<BackToLogin />}>
        <Alert tone="info">{m.sentText(sentTo)}</Alert>
        <p className="field-hint">{m.sentHint}</p>
        <Button variant="secondary" block onClick={() => setSentTo(null)}>
          {m.another}
        </Button>
      </AuthLayout>
    )
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (pending) return
    const next = validateEmail(email)
    setError(next)
    setFormError(null)
    if (next) return

    setPending(true)
    try {
      await requestPasswordReset(email.trim())
      setSentTo(email.trim())
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : m.failed)
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthLayout title={m.title} description={m.description} footer={<BackToLogin />}>
      <form onSubmit={handleSubmit} noValidate>
        <FormAlert message={formError} />
        <Input
          label={copy.email}
          type="email"
          inputMode="email"
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          maxLength={MAX_EMAIL_LENGTH}
          placeholder={copy.emailPlaceholder}
          icon={<Mail size={18} />}
          value={email}
          error={error}
          onChange={(event) => {
            setEmail(event.target.value)
            // Hata gösterildikten sonra düzeltildikçe kalkar; yazarken yeni hata çıkmaz.
            if (error) setError(validateEmail(event.target.value))
          }}
          onBlur={() => {
            if (email) setError(validateEmail(email))
          }}
        />
        <Button type="submit" size="lg" block loading={pending}>
          {pending ? m.sending : m.send}
        </Button>
      </form>
    </AuthLayout>
  )
}
