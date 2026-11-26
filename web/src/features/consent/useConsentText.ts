import { useCallback } from 'react'
import { getConsentText, type ConsentText, type ConsentType } from '../../api/consentTexts'
import { useResource, type Resource } from '../../hooks/useResource'

/** Türün güncel rıza metni. Rıza isteği bu metnin `version`'ıyla gönderilir (K-32). */
export function useConsentText(type: ConsentType): Resource<ConsentText | undefined> {
  const load = useCallback((signal: AbortSignal) => getConsentText(type, signal), [type])
  return useResource(load, 'Aydınlatma metni yüklenemedi.')
}
