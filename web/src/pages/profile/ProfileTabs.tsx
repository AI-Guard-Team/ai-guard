import { NavLink } from 'react-router'
import { useAuth } from '../../auth/useAuth'
import { defineMessages, useMessages } from '../../i18n/messages'
import './ProfileTabs.css'

const messages = defineMessages({
  tr: { sections: 'Profil bölümleri', account: 'Hesap ve güvenlik', photo: 'Kayıt fotoğrafı ve biyometrik veri' },
  en: { sections: 'Profile sections', account: 'Account and security', photo: 'Reference photo and biometric data' },
})

/** Profil alt sayfaları. Kayıt fotoğrafı yalnızca adaya açık (S2), diğer rollere sekme çıkmaz. */
export function ProfileTabs() {
  const m = useMessages(messages)
  const { user } = useAuth()
  if (user?.role !== 'Candidate') return null

  return (
    <nav className="profile-tabs" aria-label={m.sections}>
      <NavLink to="/profile" end>
        {m.account}
      </NavLink>
      <NavLink to="/profile/photo">{m.photo}</NavLink>
    </nav>
  )
}
