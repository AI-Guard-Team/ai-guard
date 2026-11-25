import { NavLink } from 'react-router'
import { useAuth } from '../../auth/useAuth'
import './ProfileTabs.css'

/** Profil alt sayfaları. Kayıt fotoğrafı yalnızca adaya açık (S2), diğer rollere sekme çıkmaz. */
export function ProfileTabs() {
  const { user } = useAuth()
  if (user?.role !== 'Candidate') return null

  return (
    <nav className="profile-tabs" aria-label="Profil bölümleri">
      <NavLink to="/profile" end>
        Hesap ve güvenlik
      </NavLink>
      <NavLink to="/profile/photo">Kayıt fotoğrafı ve biyometrik veri</NavLink>
    </nav>
  )
}
