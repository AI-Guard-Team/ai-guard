import { defineMessages, pick } from '../i18n/messages'
import { apiRequest } from './client'

const messages = defineMessages({
  tr: {
    permission:
      'Sınav güvenlik uygulamasıyla bağlantı kuruluyor. Tarayıcınız "Bu cihazdaki diğer uygulamalara ve hizmetlere erişin" izni isterse “İzin ver”i seçin. Yanlışlıkla “Engelle”yi seçtiyseniz adres çubuğunun solundaki simgeye tıklayıp izni açın ve sayfayı yenileyin.',
    noCode: 'Eşleştirme kodu alınamadı. Yeniden deneyin.',
    rejected: 'Eşleştirme kodu kabul edilmedi. Yeniden deneyin.',
    forbidden: 'Bu sayfanın adresi güvenlik ajanında izinli değil. Ajanı sınav sitesinin adresiyle yeniden başlatın.',
    unreachableServer: 'Güvenlik ajanı sunucuya ulaşamadı. Ajanı açık tutup yeniden deneyin.',
    failed: 'Güvenlik ajanı eşleştirmeyi tamamlayamadı. Yeniden deneyin.',
    notFound: 'Güvenlik ajanı bulunamadı; ajanı çalıştırıp yeniden deneyin.',
  },
  en: {
    permission:
      'Connecting to the exam security app. If your browser asks for permission to "access other apps and services on this device", choose Allow. If you chose Block by mistake, click the icon on the left of the address bar, allow access and reload the page.',
    noCode: 'A pairing code could not be obtained. Try again.',
    rejected: 'The pairing code was not accepted. Try again.',
    forbidden: 'This page’s address is not allowed by the security agent. Restart the agent with the exam site’s address.',
    unreachableServer: 'The security agent could not reach the server. Keep the agent open and try again.',
    failed: 'The security agent could not complete pairing. Try again.',
    notFound: 'The security agent was not found; start the agent and try again.',
  },
})

export const AGENT_PAIR_URL = 'http://127.0.0.1:47321/pair'
export const AGENT_PAIR_TIMEOUT_MS = 30_000

export type PairingFailure = 'timeout' | 'unreachable' | 'rejected' | 'unavailable' | 'forbidden'

export class PairingError extends Error {
  readonly kind: PairingFailure

  constructor(kind: PairingFailure, message: string) {
    super(message)
    this.name = 'PairingError'
    this.kind = kind
  }
}

/**
 * Windows kurulum paketinin yayın adresi. Paket henüz bir adreste yok; adres belli olunca
 * sınav sayfasındaki "Güvenlik ajanını indir" bağlantısı buraya bağlanır (K-25).
 */
export const AGENT_DOWNLOAD_URL = ''

async function issueCode(sessionId: string): Promise<string> {
  const data = await apiRequest<{ code?: string }>(`/api/sessions/${sessionId}/agent-code`, { method: 'POST' })
  if (!data?.code) throw new PairingError('rejected', pick(messages).noCode)
  return data.code
}

async function postToLocalAgent(code: string, timeoutMs: number): Promise<void> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(AGENT_PAIR_URL, {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ code }),
      signal: controller.signal,
    })
    if (response.ok) return
    if (response.status === 409) {
      throw new PairingError('rejected', pick(messages).rejected)
    }
    if (response.status === 403) {
      throw new PairingError('forbidden', pick(messages).forbidden)
    }
    if (response.status === 502) {
      throw new PairingError('unavailable', pick(messages).unreachableServer)
    }
    throw new PairingError('unavailable', pick(messages).failed)
  } catch (error) {
    if (error instanceof PairingError) throw error
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new PairingError('timeout', pick(messages).permission)
    }
    // Ajan kapalıyken fetch hata fırlatır; izin reddi tarayıcıda bundan ayırt edilemez.
    throw new PairingError('unreachable', `${pick(messages).notFound} ${pick(messages).permission}`)
  } finally {
    clearTimeout(timer)
  }
}

/** Kodu ekrana yazmadan ajana gönderir. Reddedilen kodda bir kez yeni kod alınıp tekrar denenir. */
export async function pairAgent(sessionId: string, timeoutMs = AGENT_PAIR_TIMEOUT_MS): Promise<void> {
  let rejected: PairingError | null = null
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const code = await issueCode(sessionId)
    try {
      await postToLocalAgent(code, timeoutMs)
      return
    } catch (error) {
      if (error instanceof PairingError && error.kind === 'rejected' && attempt === 0) {
        rejected = error
        continue
      }
      throw error
    }
  }
  throw rejected ?? new PairingError('rejected', pick(messages).rejected)
}
