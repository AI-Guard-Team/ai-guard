import { apiRequest } from './client'

/**
 * `POST /api/sessions/{id}/consent` (A3). `textVersion` ekranda gösterilen oturum rıza metninin sürümü
 * (`GET /api/consent-texts/Session`); eskiyse sunucu 400 döner. Yanıt yeni durum: AgentPairing ya da Cancelled.
 */
export function submitSessionConsent(sessionId: string, granted: boolean, textVersion: string): Promise<{ state?: string } | undefined> {
  return apiRequest(`/api/sessions/${sessionId}/consent`, {
    method: 'POST',
    body: { accepted: granted, textVersion },
  })
}

/**
 * `POST /api/sessions/{id}/verify` (A3). Başarılıysa sınav başlar (süre ve hak burada işlemeye başlar, K-24)
 * ya da kesintiden dönülür. 422: canlı yüz yok / eşleşmedi, 429: çok sık deneme, 503: görüntü servisi yok.
 */
export function verifyIdentity(sessionId: string, frames: Blob[]): Promise<{ state?: string } | undefined> {
  const body = new FormData()
  frames.forEach((frame, index) => body.append('frames', frame, `kare-${index + 1}.jpg`))
  return apiRequest(`/api/sessions/${sessionId}/verify`, { method: 'POST', body })
}

/** Eşleşme adımında durum bu sıklıkla yenilenir (K-25: birkaç saniye). */
export const AGENT_STATUS_POLL_MS = 3_000

export type AgentContinueMode = 'noAgent' | 'indicated'

export interface AgentIndicatorView {
  type: string
  name: string
  message: string
  fix: string
}

/** `GET /api/sessions/{id}/agent` (K-25). Devam uçundan ayrıdır. */
export interface AgentStatus {
  state: string
  paired: boolean
  agentActive: boolean
  lastSignalAt?: string | null
  indicators: AgentIndicatorView[]
  canContinueIndicated: boolean
  canContinueWithoutAgent: boolean
}

export function getAgentStatus(sessionId: string): Promise<AgentStatus | undefined> {
  return apiRequest<AgentStatus>(`/api/sessions/${sessionId}/agent`)
}

/** Gerekçe `ExamSession.ContinueJustification` alanına gider. `noAgent` ajan çalışmıyorsa, `indicated` gösterge varken. */
export function continueAgent(sessionId: string, mode: AgentContinueMode, reason: string): Promise<{ state?: string } | undefined> {
  return apiRequest(`/api/sessions/${sessionId}/agent/continue`, {
    method: 'POST',
    body: { mode, reason },
  })
}

/** Sınav sürerken oturum bu sıklıkla yenilenir. 30 sn istek gelmezse oturum Disconnected sayılıyor (K-24). */
export const SESSION_POLL_MS = 10_000

export interface SessionQuestion {
  id: string
  text: string
  options: string[]
  order: number
}

export interface SessionAnswer {
  questionId: string
  selectedOption: number | null
  seq: number
}

/** `GET /api/sessions/{id}` (K-24). Sorular yalnızca InProgress'te dolu; doğru seçenek yok. */
export interface SessionView {
  id: string
  examId: string
  examName: string
  state: string
  durationMinutes: number
  startedAt?: string | null
  endsAt?: string | null
  serverTime: string
  questions: SessionQuestion[]
  answers: SessionAnswer[]
  lastSeq: number
}

export interface AnswerPayload {
  questionId: string
  selectedOption: number | null
  seq: number
  clientTime: string
}

export function getSession(sessionId: string): Promise<SessionView | undefined> {
  return apiRequest<SessionView>(`/api/sessions/${sessionId}`)
}

/** 201 yeni kayıt, 200 aynı seq'in tekrarı. İkisi de kuyruktan düşürülebilir. */
export function saveAnswer(sessionId: string, answer: AnswerPayload): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/answers`, { method: 'POST', body: answer })
}

export function submitExam(sessionId: string): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/submit`, { method: 'POST' })
}

/** Tarayıcının bildirebildiği olaylar (K-16). PERMISSION_REVOKED'i sunucu kendisi yazıyor (K-30). */
export type BrowserEventType = 'TAB_BLUR' | 'FULLSCREEN_EXIT' | 'COPY_PASTE' | 'RIGHT_CLICK'

/** `POST /api/sessions/{id}/events`: gövde yalnızca tür ve süre; kaynak ve zaman sunucuda (K-16). */
export function sendBrowserEvent(sessionId: string, type: BrowserEventType, durationSec: number): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/events`, { method: 'POST', body: { type, durationSec } })
}

/** `POST /api/sessions/{id}/frames` (S4): saniyede bir JPEG kare. Yanıt boş; analiz sonucu adaya dönmez. */
export function sendFrame(sessionId: string, frame: Blob): Promise<void> {
  const body = new FormData()
  body.append('frame', frame, 'kare.jpg')
  return apiRequest(`/api/sessions/${sessionId}/frames`, { method: 'POST', body })
}

/** `POST /api/sessions/{id}/audio` (S4): ~1 sn'lik WebM/Opus parça (S-22). */
export function sendAudio(sessionId: string, chunk: Blob): Promise<void> {
  const body = new FormData()
  body.append('audio', chunk, 'ses.webm')
  return apiRequest(`/api/sessions/${sessionId}/audio`, { method: 'POST', body })
}

/**
 * `POST /api/sessions/{id}/permission` (S4, FR-17). `false`: sınav izin beklemeye geçer, sorular gizlenir.
 * `true`: sınava dönülür; izinsiz süreyi sunucu ölçer (K-30), istemci süre göndermez.
 */
export function reportPermission(sessionId: string, granted: boolean): Promise<{ state?: string } | undefined> {
  return apiRequest(`/api/sessions/${sessionId}/permission`, { method: 'POST', body: { granted } })
}
