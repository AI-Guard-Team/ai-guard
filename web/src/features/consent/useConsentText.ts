import { useCallback } from 'react'
import { getConsentText, type ConsentText, type ConsentType } from '../../api/consentTexts'
import { useResource, type Resource } from '../../hooks/useResource'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: { failed: 'Aydınlatma metni yüklenemedi.' },
  en: { failed: 'The privacy notice could not be loaded.' },
})

/** Türün güncel rıza metni. Rıza isteği bu metnin `version`'ıyla gönderilir (K-32). */
export function useConsentText(type: ConsentType): Resource<ConsentText | undefined> {
  const m = useMessages(messages)
  const load = useCallback((signal: AbortSignal) => getConsentText(type, signal), [type])
  return useResource(load, m.failed)
}
