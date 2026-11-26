import { defineMessages } from '../../i18n/messages'

/** Giriş ekranlarında tekrar eden metinler: e-posta alanı, geri dönüş bağlantısı, ortak düğmeler. */
export const authCopy = defineMessages({
  tr: {
    email: 'E-posta',
    emailPlaceholder: 'ad.soyad@kurum.edu.tr',
    password: 'Şifre',
    newPassword: 'Yeni şifre',
    backToLogin: 'Giriş sayfasına dön',
    goToLogin: 'Giriş sayfasına git',
    saving: 'Kaydediliyor…',
    passwordReset: 'Şifre yenileme',
    activation: 'Hesap etkinleştirme',
  },
  en: {
    email: 'Email',
    emailPlaceholder: 'name.surname@school.edu',
    password: 'Password',
    newPassword: 'New password',
    backToLogin: 'Back to sign in',
    goToLogin: 'Go to sign in',
    saving: 'Saving…',
    passwordReset: 'Password reset',
    activation: 'Account activation',
  },
})
