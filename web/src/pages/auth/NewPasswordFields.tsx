import { Check, Circle } from 'lucide-react'
import { PasswordInput } from '../../components/Input'
import { passwordChecks, type NewPasswordErrors } from './passwordRules'
import './NewPasswordFields.css'

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
export function NewPasswordFields({
  password,
  confirm,
  errors,
  onPasswordChange,
  onConfirmChange,
  passwordLabel = 'Yeni şifre',
}: NewPasswordFieldsProps) {
  const checks = passwordChecks(password, confirm)
  return (
    <>
      <PasswordInput
        label={passwordLabel}
        autoComplete="new-password"
        value={password}
        error={errors.password}
        onChange={(event) => onPasswordChange(event.target.value)}
      />
      <PasswordInput
        label={`${passwordLabel} (tekrar)`}
        autoComplete="new-password"
        value={confirm}
        error={errors.confirm}
        onChange={(event) => onConfirmChange(event.target.value)}
      />
      <ul className="password-checks" aria-label="Şifre kuralları">
        {checks.map((check) => (
          <li key={check.id} className={check.met ? 'is-met' : undefined}>
            {check.met ? <Check size={16} aria-hidden="true" /> : <Circle size={16} aria-hidden="true" />}
            <span>{check.label}</span>
            <span className="visually-hidden">{check.met ? ' (sağlandı)' : ' (henüz sağlanmadı)'}</span>
          </li>
        ))}
      </ul>
    </>
  )
}
