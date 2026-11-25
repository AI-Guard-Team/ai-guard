import { ShieldCheck } from 'lucide-react'
import { ROLE_LABEL } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import './ProfilePage.css'

/** Profil kartı: avatar, ad, e-posta, rol. Profilin iki sekmesinde (hesap, kayıt fotoğrafı) aynı (K-43). */
export function ProfileHero() {
  const { user } = useAuth()
  if (!user) return null
  const initials = (user.name ?? user.email ?? '?')
    .trim()
    .split(/\s+/)
    .filter((part) => /^\p{L}/u.test(part))
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toLocaleUpperCase('tr-TR')

  return (
    <section className="profile-hero" aria-label="Hesabınız">
      {/* Sayfa başlığı ekran okuyucu için; görünen başlık kullanıcının adı. */}
      <h1 className="visually-hidden">Profil</h1>
      <span className="profile-avatar" aria-hidden="true">
        {initials || '?'}
      </span>
      <div className="profile-hero-text">
        <p className="profile-name">{user.name?.trim() || 'Adınız kayıtlı değil'}</p>
        <p>{user.email ?? ''}</p>
      </div>
      <span className="profile-role">
        <ShieldCheck size={16} aria-hidden="true" />
        {ROLE_LABEL[user.role]}
      </span>
    </section>
  )
}
