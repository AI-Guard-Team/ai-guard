import { Check, Circle } from 'lucide-react'
import { PasswordInput } from '../../components/Input'
import { defineMessages, useMessages } from '../../i18n/messages'
import { authCopy } from './authCopy'
import { MAX_PASSWORD_LENGTH, passwordChecks, type NewPasswordErrors } from './passwordRules'
import './NewPasswordFields.css'

const messages = defineMessages({
  tr: {
    repeat: (label: string) => `${label} (tekrar)`,
    rules: 'Şifre kuralları',
    met: ' (sağlandı)',
    unmet: ' (henüz sağlanmadı)',
  },
  en: {
    repeat: (label: string) => `Repeat ${label.toLowerCase()}`,
    rules: 'Password rules',
    met: ' (met)',
    unmet: ' (not met yet)',
  },
})

interface NewPasswordFieldsProps {
  password: string
  confirm: string
  errors: NewPasswordErrors
  onPasswordChange: (value: string) => void
  onConfirmChange: (value: string) => void
  passwordLabel?: string
}

/**
 * Yeni şifre + tekrar + canlı kural listesi. Hesap etkinleştirme, şifre sıfırlama ve profilde aynı bileşen.
 * Stitch'teki "büyük harf, özel karakter" maddeleri yok: sunucu yalnızca uzunluk istiyor ve
 * sunucunun istemediği bir kuralı ekranda göstermek kullanıcıyı yanıltır.
 */
export function NewPasswordFields({ password, confirm, errors, onPasswordChange, onConfirmChange, passwordLabel }: NewPasswordFieldsProps) {
  const m = useMessages(messages)
  const copy = useMessages(authCopy)
  const label = passwordLabel ?? copy.newPassword
  const checks = passwordChecks(password, confirm)
  return (
    <>
      <PasswordInput
        label={label}
        autoComplete="new-password"
        maxLength={MAX_PASSWORD_LENGTH}
        value={password}
        error={errors.password}
        onChange={(event) => onPasswordChange(event.target.value)}
      />
      <PasswordInput
        label={m.repeat(label)}
        autoComplete="new-password"
        maxLength={MAX_PASSWORD_LENGTH}
        value={confirm}
        error={errors.confirm}
        onChange={(event) => onConfirmChange(event.target.value)}
      />
      <ul className="password-checks" aria-label={m.rules}>
        {checks.map((check) => (
          <li key={check.id} className={check.met ? 'is-met' : undefined}>
            {check.met ? <Check size={16} aria-hidden="true" /> : <Circle size={16} aria-hidden="true" />}
            <span>{check.label}</span>
            <span className="visually-hidden">{check.met ? m.met : m.unmet}</span>
          </li>
        ))}
      </ul>
    </>
  )
}
