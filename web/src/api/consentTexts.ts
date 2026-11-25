import { apiRequest } from './client'

export type ConsentType = 'Biometric' | 'Session'

/** `GET /api/consent-texts/{type}` (A5, K-32). Rıza isteği bu `version`'ı geri göndermeli; sunucu yalnızca güncel sürümü kabul ediyor. */
export interface ConsentText {
  type: ConsentType
  version: string
  title: string
  body: string
  publishedAt: string
}

export function getConsentText(type: ConsentType, signal?: AbortSignal): Promise<ConsentText | undefined> {
  return apiRequest<ConsentText>(`/api/consent-texts/${type}`, { signal })
}
