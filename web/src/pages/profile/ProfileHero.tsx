import { ShieldCheck } from 'lucide-react'
import { ROLE_LABEL } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import './ProfilePage.css'

const messages = defineMessages({
  tr: { account: 'Hesabınız', profile: 'Profil', noName: 'Adınız kayıtlı değil' },
  en: { account: 'Your account', profile: 'Profile', noName: 'No name on record' },
})

/** Profil kartı: avatar, ad, e-posta, rol. Profilin iki sekmesinde (hesap, kayıt fotoğrafı) aynı (K-44). */
export function ProfileHero() {
  const m = useMessages(messages)
  const { user } = useAuth()
  if (!user) return null
  const initials = (user.name ?? user.email ?? '?')
    .trim()
    .split(/\s+/)
    .filter((part) => /^\p{L}/u.test(part))
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toLocaleUpperCase(getLocale())

  return (
    <section className="profile-hero" aria-label={m.account}>
      {/* Sayfa başlığı ekran okuyucu için; görünen başlık kullanıcının adı. */}
      <h1 className="visually-hidden">{m.profile}</h1>
      <span className="profile-avatar" aria-hidden="true">
        {initials || '?'}
      </span>
      <div className="profile-hero-text">
        <p className="profile-name">{user.name?.trim() || m.noName}</p>
        <p>{user.email ?? ''}</p>
      </div>
      <span className="profile-role">
        <ShieldCheck size={16} aria-hidden="true" />
        {ROLE_LABEL[user.role]}
      </span>
    </section>
  )
}
