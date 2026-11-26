// Rol adları arka uçtaki UserRole enum'uyla birebir aynıdır (E2); değiştirmeden önce ekibe sorulur.
export const ROLES = ['Admin', 'Reviewer', 'Candidate'] as const

export type Role = (typeof ROLES)[number]

export function isRole(value: unknown): value is Role {
  return typeof value === 'string' && (ROLES as readonly string[]).includes(value)
}

export const ROLE_HOME: Record<Role, string> = {
  Admin: '/overview',
  Reviewer: '/overview',
  Candidate: '/overview',
}

export const ROLE_LABEL: Record<Role, string> = {
  Admin: 'Yönetici',
  Reviewer: 'Değerlendirici',
  Candidate: 'Aday',
}

export interface MenuItem {
  to: string
  label: string
  /** true: yalnızca tam adreste etkin görünür (alt sayfalarda değil). */
  end?: boolean
}

const OVERVIEW: MenuItem = { to: '/overview', label: 'Genel bakış' }

export const ROLE_MENU: Record<Role, MenuItem[]> = {
  Admin: [
    OVERVIEW,
    { to: '/admin/exams', label: 'Sınavlar' },
    { to: '/admin/users', label: 'Kullanıcılar' },
    { to: '/admin/reports', label: 'Raporlar' },
    { to: '/admin/settings', label: 'Ayarlar' },
    { to: '/admin/audit', label: 'Denetim kaydı' },
  ],
  Reviewer: [OVERVIEW, { to: '/review', label: 'İncelemeler' }],
  Candidate: [OVERVIEW, { to: '/exams', label: 'Sınavlarım' }, { to: '/profile/photo', label: 'Kayıt fotoğrafı' }],
}
