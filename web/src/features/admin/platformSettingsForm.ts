import type { PlatformSettings, PlatformSettingsRequest } from '../../api/management'
import { isHttpsUrl } from '../../lib/safeUrl'

export { isHttpsUrl }

// Platform ayarları formunun doğrulaması; kurallar sunucudakiyle aynı (PlatformSettingsService, K-49).

export const LIMITS = { text: 4000, email: 254, url: 500 } as const

export type PlatformField = keyof PlatformSettingsRequest
export type PlatformErrorCode = 'tooLong' | 'email' | 'phone' | 'https'
export type PlatformDraft = Record<PlatformField, string>

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const PHONE = /^[0-9 +()-]{3,40}$/

export function toDraft(settings: PlatformSettings): PlatformDraft {
  return {
    alternativeMethodText: settings.alternativeMethodText ?? '',
    supportEmail: settings.supportEmail ?? '',
    supportPhone: settings.supportPhone ?? '',
    agentDownloadUrlWindows: settings.agentDownloadUrlWindows ?? '',
    agentDownloadUrlMac: settings.agentDownloadUrlMac ?? '',
  }
}

/** Boş alan "ayarlanmadı" (null); dolu alan baştaki ve sondaki boşluklardan arındırılır. */
export function toRequest(draft: PlatformDraft): PlatformSettingsRequest {
  const clean = (value: string) => (value.trim() === '' ? null : value.trim())
  return {
    alternativeMethodText: clean(draft.alternativeMethodText),
    supportEmail: clean(draft.supportEmail),
    supportPhone: clean(draft.supportPhone),
    agentDownloadUrlWindows: clean(draft.agentDownloadUrlWindows),
    agentDownloadUrlMac: clean(draft.agentDownloadUrlMac),
  }
}

export function validate(draft: PlatformDraft): Partial<Record<PlatformField, PlatformErrorCode>> {
  const errors: Partial<Record<PlatformField, PlatformErrorCode>> = {}
  const v = toRequest(draft)
  if (v.alternativeMethodText && v.alternativeMethodText.length > LIMITS.text) errors.alternativeMethodText = 'tooLong'
  if (v.supportEmail && (v.supportEmail.length > LIMITS.email || !EMAIL.test(v.supportEmail))) errors.supportEmail = 'email'
  if (v.supportPhone && !PHONE.test(v.supportPhone)) errors.supportPhone = 'phone'
  for (const field of ['agentDownloadUrlWindows', 'agentDownloadUrlMac'] as const) {
    const url = v[field]
    if (url && (url.length > LIMITS.url || !isHttpsUrl(url))) errors[field] = 'https'
  }
  return errors
}
