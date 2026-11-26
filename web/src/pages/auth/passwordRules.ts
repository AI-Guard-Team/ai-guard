import { defineMessages, pick } from '../../i18n/messages'

// Sunucudaki kuralla aynı (api AuthPolicy.ValidatePassword, MIMARI-KARARLAR K-20): uzunluk, karmaşıklık değil (NIST SP 800-63B).
export const MIN_PASSWORD_LENGTH = 8
export const MAX_PASSWORD_LENGTH = 128
/** RFC 5321: bir e-posta adresi en fazla 254 karakter. */
export const MAX_EMAIL_LENGTH = 254

// Basit biçim kontrolü: "@" iki yanında boşluksuz metin ve alan adında nokta. Asıl doğrulama sunucuda.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const messages = defineMessages({
  tr: {
    length: `${MIN_PASSWORD_LENGTH}–${MAX_PASSWORD_LENGTH} karakter`,
    notBlank: 'Yalnızca boşluktan oluşmuyor',
    match: 'İki alan aynı',
    passwordRequired: 'Şifre zorunludur.',
    passwordShort: `Şifre en az ${MIN_PASSWORD_LENGTH} karakter olmalıdır.`,
    passwordLong: `Şifre en fazla ${MAX_PASSWORD_LENGTH} karakter olabilir.`,
    passwordBlank: 'Şifre yalnızca boşluktan oluşamaz.',
    confirmRequired: 'Şifre tekrarı zorunludur.',
    confirmMismatch: 'Şifreler aynı olmalıdır.',
    emailRequired: 'E-posta zorunludur.',
    emailInvalid: 'Geçerli bir e-posta adresi girin (ör. ad.soyad@kurum.edu.tr).',
    emailLong: `E-posta en fazla ${MAX_EMAIL_LENGTH} karakter olabilir.`,
  },
  en: {
    length: `${MIN_PASSWORD_LENGTH}–${MAX_PASSWORD_LENGTH} characters`,
    notBlank: 'Not only spaces',
    match: 'Both fields match',
    passwordRequired: 'Password is required.',
    passwordShort: `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`,
    passwordLong: `Password can be at most ${MAX_PASSWORD_LENGTH} characters.`,
    passwordBlank: 'Password cannot be only spaces.',
    confirmRequired: 'Please repeat the password.',
    confirmMismatch: 'Passwords must match.',
    emailRequired: 'Email is required.',
    emailInvalid: 'Enter a valid email address (e.g. name.surname@school.edu).',
    emailLong: `Email can be at most ${MAX_EMAIL_LENGTH} characters.`,
  },
})

export interface PasswordCheck {
  id: 'length' | 'notBlank' | 'match'
  label: string
  met: boolean
}

export function passwordChecks(password: string, confirm: string): PasswordCheck[] {
  const m = pick(messages)
  return [
    { id: 'length', label: m.length, met: password.length >= MIN_PASSWORD_LENGTH && password.length <= MAX_PASSWORD_LENGTH },
    { id: 'notBlank', label: m.notBlank, met: password.trim().length > 0 },
    { id: 'match', label: m.match, met: confirm.length > 0 && confirm === password },
  ]
}

export interface NewPasswordErrors {
  password?: string
  confirm?: string
}

/** Formu göndermeden önceki kontrol; kurallar sunucununkilerle aynı. */
export function validateNewPassword(password: string, confirm: string): NewPasswordErrors {
  const m = pick(messages)
  let passwordError: string | undefined
  if (!password) passwordError = m.passwordRequired
  else if (password.length < MIN_PASSWORD_LENGTH) passwordError = m.passwordShort
  else if (password.length > MAX_PASSWORD_LENGTH) passwordError = m.passwordLong
  else if (!password.trim()) passwordError = m.passwordBlank

  const confirmError = !confirm ? m.confirmRequired : confirm !== password ? m.confirmMismatch : undefined
  return { password: passwordError, confirm: confirmError }
}

/** Giriş şifresi: yalnızca boş olmasın (eski kurallarla açılmış hesaplar da girebilsin). */
export function validateLoginPassword(password: string): string | undefined {
  return password ? undefined : pick(messages).passwordRequired
}

/** E-posta: zorunlu, en fazla 254 karakter, basit biçim. Boşluklar kırpılarak denetlenir. */
export function validateEmail(value: string): string | undefined {
  const m = pick(messages)
  const email = value.trim()
  if (!email) return m.emailRequired
  if (email.length > MAX_EMAIL_LENGTH) return m.emailLong
  return EMAIL_PATTERN.test(email) ? undefined : m.emailInvalid
}
