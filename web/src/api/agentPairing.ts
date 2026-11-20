import { apiRequest } from './client'

export const AGENT_PAIR_URL = 'http://127.0.0.1:47321/pair'
export const AGENT_PAIR_TIMEOUT_MS = 30_000

const PERMISSION_GUIDANCE =
  'Sınav güvenlik uygulamasıyla bağlantı kuruluyor. Tarayıcınız "Bu cihazdaki diğer uygulamalara ve hizmetlere erişin" izni isterse İzin ver\'i seçin. Yanlışlıkla Engelle\'yi seçtiyseniz adres çubuğunun solundaki simgeye tıklayıp izni açın ve sayfayı yenileyin.'

export type PairingFailure = 'timeout' | 'unreachable' | 'rejected' | 'unavailable' | 'forbidden'

export class PairingError extends Error {
  readonly kind: PairingFailure

  constructor(kind: PairingFailure, message: string) {
    super(message)
    this.name = 'PairingError'
    this.kind = kind
  }
}

export function canContinueWithoutAgent(kind: PairingFailure): boolean {
  return kind === 'timeout' || kind === 'unreachable' || kind === 'unavailable' || kind === 'forbidden'
}

async function issueCode(sessionId: string): Promise<string> {
  const data = await apiRequest<{ code?: string }>(`/api/sessions/${sessionId}/agent-code`, { method: 'POST' })
  if (!data?.code) throw new PairingError('rejected', 'Eşleştirme kodu alınamadı. Yeniden deneyin.')
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
      throw new PairingError('rejected', 'Eşleştirme kodu kabul edilmedi. Yeniden deneyin.')
    }
    if (response.status === 403) {
      throw new PairingError('forbidden', 'Bu sayfanın adresi güvenlik ajanında izinli değil. Ajanı sınav sitesinin adresiyle yeniden başlatın.')
    }
    if (response.status === 502) {
      throw new PairingError('unavailable', 'Güvenlik ajanı sunucuya ulaşamadı. Ajanı açık tutup yeniden deneyin.')
    }
    throw new PairingError('unavailable', 'Güvenlik ajanı eşleştirmeyi tamamlayamadı. Yeniden deneyin.')
  } catch (error) {
    if (error instanceof PairingError) throw error
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new PairingError('timeout', PERMISSION_GUIDANCE)
    }
    // Ajan kapalıyken fetch hata fırlatır; izin reddi tarayıcıda bundan ayırt edilemez.
    throw new PairingError(
      'unreachable',
      `Güvenlik ajanı bulunamadı; ajanı çalıştırıp yeniden deneyin. ${PERMISSION_GUIDANCE}`,
    )
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
  throw rejected ?? new PairingError('rejected', 'Eşleştirme kodu kabul edilmedi. Yeniden deneyin.')
}
