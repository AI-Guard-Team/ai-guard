import { defineMessages, live } from '../i18n/messages'

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

export const ROLE_LABEL: Record<Role, string> = live(
  defineMessages<Record<Role, string>>({
    tr: { Admin: 'Yönetici', Reviewer: 'Değerlendirici', Candidate: 'Aday' },
    en: { Admin: 'Administrator', Reviewer: 'Reviewer', Candidate: 'Candidate' },
  }),
)

export interface MenuItem {
  to: string
  label: string
  /** true: yalnızca tam adreste etkin görünür (alt sayfalarda değil). */
  end?: boolean
}

export const ROLE_MENU: Record<Role, MenuItem[]> = live(
  defineMessages<Record<Role, MenuItem[]>>({
    tr: {
      Admin: [
        { to: '/overview', label: 'Genel bakış' },
        { to: '/admin/exams', label: 'Sınavlar' },
        { to: '/admin/users', label: 'Kullanıcılar' },
        { to: '/admin/reports', label: 'Raporlar' },
        { to: '/admin/settings', label: 'Ayarlar' },
        { to: '/admin/audit', label: 'Denetim kaydı' },
      ],
      Reviewer: [
        { to: '/overview', label: 'Genel bakış' },
        { to: '/review', label: 'İncelemeler' },
      ],
      Candidate: [
        { to: '/overview', label: 'Genel bakış' },
        { to: '/exams', label: 'Sınavlarım' },
        { to: '/profile/photo', label: 'Kayıt fotoğrafı' },
      ],
    },
    en: {
      Admin: [
        { to: '/overview', label: 'Overview' },
        { to: '/admin/exams', label: 'Exams' },
        { to: '/admin/users', label: 'Users' },
        { to: '/admin/reports', label: 'Reports' },
        { to: '/admin/settings', label: 'Settings' },
        { to: '/admin/audit', label: 'Audit log' },
      ],
      Reviewer: [
        { to: '/overview', label: 'Overview' },
        { to: '/review', label: 'Reviews' },
      ],
      Candidate: [
        { to: '/overview', label: 'Overview' },
        { to: '/exams', label: 'My exams' },
        { to: '/profile/photo', label: 'Reference photo' },
      ],
    },
  }),
)
