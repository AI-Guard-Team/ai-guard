// Rol adları arka uçtaki UserRole enum'uyla birebir aynıdır (E2); değiştirmeden önce ekibe sorulur.
export const ROLES = ['Admin', 'Reviewer', 'Candidate'] as const

export type Role = (typeof ROLES)[number]

export function isRole(value: unknown): value is Role {
  return typeof value === 'string' && (ROLES as readonly string[]).includes(value)
}

export const ROLE_HOME: Record<Role, string> = {
  Admin: '/admin',
  Reviewer: '/review',
  Candidate: '/exams',
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

export const ROLE_MENU: Record<Role, MenuItem[]> = {
  Admin: [{ to: '/admin', label: 'Yönetim' }],
  Reviewer: [{ to: '/review', label: 'İncelemeler' }],
  Candidate: [{ to: '/exams', label: 'Sınavlarım' }],
}
