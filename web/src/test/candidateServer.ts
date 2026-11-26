import { jsonResponse, type FetchHandler } from './fetch'

/** Testler için sunucudaki rıza metinleri (A5 K-32 ile aynı biçim). */
export const CONSENT_TEXTS = {
  Biometric: {
    type: 'Biometric',
    version: 'kvkk-biometric-v2',
    title: 'Biyometrik veri aydınlatma metni',
    body: 'Birinci paragraf.\n\nİkinci paragraf.',
    publishedAt: '2026-11-01T00:00:00Z',
  },
  Session: {
    type: 'Session',
    version: 'kvkk-session-v3',
    title: 'Sınav izleme aydınlatma metni',
    body: 'Sınav sırasında kamera ve mikrofon analiz edilir.\n\nKanıt kareleri şifreli saklanır.',
    publishedAt: '2026-11-01T00:00:00Z',
  },
} as const

/**
 * Rıza metni ve biyometrik durum isteklerini yanıtlar; diğer istekler `next`'e gider.
 * Böylece her test yalnızca kendi ilgilendiği ucu yazar.
 */
export function withCandidateBasics(next: FetchHandler, options: { enrolled?: boolean } = {}): FetchHandler {
  return (url, init) => {
    if (url === '/api/consent-texts/Biometric') return jsonResponse(200, CONSENT_TEXTS.Biometric)
    if (url === '/api/consent-texts/Session') return jsonResponse(200, CONSENT_TEXTS.Session)
    if (url === '/api/me/biometric' && (init.method ?? 'GET') === 'GET') {
      return jsonResponse(200, options.enrolled ? { enrolled: true, enrolledAt: '2026-11-20T10:00:00Z', consentTextVersion: 'kvkk-biometric-v2' } : { enrolled: false })
    }
    return next(url, init)
  }
}
