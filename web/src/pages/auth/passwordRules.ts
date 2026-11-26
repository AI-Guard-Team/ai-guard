// Sunucudaki kuralla aynı (api AuthPolicy.ValidatePassword, MIMARI-KARARLAR K-20): uzunluk, karmaşıklık değil (NIST SP 800-63B).
export const MIN_PASSWORD_LENGTH = 8
export const MAX_PASSWORD_LENGTH = 128

export interface PasswordCheck {
  id: 'length' | 'notBlank' | 'match'
  label: string
  met: boolean
}

export function passwordChecks(password: string, confirm: string): PasswordCheck[] {
  return [
    {
      id: 'length',
      label: `${MIN_PASSWORD_LENGTH}–${MAX_PASSWORD_LENGTH} karakter`,
      met: password.length >= MIN_PASSWORD_LENGTH && password.length <= MAX_PASSWORD_LENGTH,
    },
    { id: 'notBlank', label: 'Yalnızca boşluktan oluşmuyor', met: password.trim().length > 0 },
    { id: 'match', label: 'İki alan aynı', met: confirm.length > 0 && confirm === password },
  ]
}

export interface NewPasswordErrors {
  password?: string
  confirm?: string
}

/** Formu göndermeden önceki kontrol; mesajlar sunucununkilerle aynı dilde. */
export function validateNewPassword(password: string, confirm: string): NewPasswordErrors {
  let passwordError: string | undefined
  if (!password) passwordError = 'Şifre zorunludur.'
  else if (password.length < MIN_PASSWORD_LENGTH) passwordError = `Şifre en az ${MIN_PASSWORD_LENGTH} karakter olmalıdır.`
  else if (password.length > MAX_PASSWORD_LENGTH) passwordError = `Şifre en fazla ${MAX_PASSWORD_LENGTH} karakter olabilir.`
  else if (!password.trim()) passwordError = 'Şifre yalnızca boşluktan oluşamaz.'

  const confirmError = !confirm ? 'Şifre tekrarı zorunludur.' : confirm !== password ? 'Şifreler aynı olmalıdır.' : undefined
  return { password: passwordError, confirm: confirmError }
}
