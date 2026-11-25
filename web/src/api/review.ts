import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { tokenStore } from '../auth/tokenStore'
import { apiRequest, refreshTokens } from './client'

// Adlar ve biçimler arka uçtaki A4 sözleşmesiyle aynı (K-31). Sabit listeler JSON'da metin olarak gelir.

export type SessionFlag = 'Risky' | 'Indicated' | 'NoAgent' | 'MonitoringGap' | 'PermissionGap'
export type ReviewStatus = 'NotStarted' | 'AwaitingReview' | 'Completed' | 'Decided'
export type ReviewSort = 'Priority' | 'Score' | 'StartedAt'
export type DecisionType = 'Clean' | 'Suspicious' | 'Violation'

export interface ExamSummary {
  id: string
  name: string
  type: string
  durationMinutes: number
  windowStartAt: string
  windowEndAt: string
}

export interface ReviewSessionRow {
  sessionId: string
  candidateId: string
  candidateName: string
  startedAt: string
  endedAt?: string | null
  riskScore: number
  flags: SessionFlag[]
  status: ReviewStatus
  decision?: DecisionType | null
  topReason?: string | null
}

export interface ReviewSessionList {
  pendingCount: number
  sessions: ReviewSessionRow[]
}

export interface ReviewEvent {
  id: string
  type: string
  source: string
  startedAt: string
  durationSec: number
  confidence?: number | null
  reason?: string | null
  contribution: number
  isFalseAlarm: boolean
  falseAlarmMarkedBy?: string | null
  falseAlarmMarkedAt?: string | null
  evidenceUrl?: string | null
}

export interface ScorePoint {
  at: string
  score: number
}

export interface ReviewDecision {
  decision: DecisionType
  note?: string | null
  reviewerName: string
  decidedAt: string
}

export interface ReviewNote {
  id: string
  text: string
  authorName: string
  createdAt: string
}

export interface ReviewSessionDetail {
  sessionId: string
  examId: string
  examName: string
  candidateId: string
  candidateName: string
  state: string
  status: ReviewStatus
  createdAt: string
  startedAt?: string | null
  endedAt?: string | null
  riskScore: number
  flags: SessionFlag[]
  decision?: ReviewDecision | null
  notes: ReviewNote[]
  events: ReviewEvent[]
  scoreHistory: ScorePoint[]
  monitoringDataDeletedAt?: string | null
}

export interface SessionListFilter {
  /** `true`: yalnızca riskli; `false`: yalnızca riskli olmayan; `undefined`: hepsi. */
  risky?: boolean
  flags?: SessionFlag[]
  sort?: ReviewSort
}

/** Atandığı sınavlar (değerlendiriciye yalnızca atandıkları döner). */
export function listReviewExams(): Promise<ExamSummary[]> {
  return apiRequest<ExamSummary[]>('/api/exams')
}

export function getReviewExam(examId: string): Promise<ExamSummary> {
  return apiRequest<ExamSummary>(`/api/exams/${examId}`)
}

export function listReviewSessions(examId: string, filter: SessionListFilter = {}, signal?: AbortSignal): Promise<ReviewSessionList> {
  const query = new URLSearchParams()
  if (filter.risky !== undefined) query.set('risky', String(filter.risky))
  filter.flags?.forEach((flag) => query.append('flags', flag))
  if (filter.sort) query.set('sort', filter.sort)
  const search = query.toString()
  return apiRequest<ReviewSessionList>(`/api/review/exams/${examId}/sessions${search ? `?${search}` : ''}`, { signal })
}

/** Yanıttaki kanıt bağlantıları 5 dakika geçerli; süresi dolunca detay yeniden istenmeli. */
export function getReviewSession(sessionId: string, signal?: AbortSignal): Promise<ReviewSessionDetail> {
  return apiRequest<ReviewSessionDetail>(`/api/review/sessions/${sessionId}`, { signal })
}

/** Yalnızca riskli veya işaretli olarak tamamlanmış oturumda ve bir kez verilir. */
export function decideSession(sessionId: string, decision: DecisionType, note: string): Promise<void> {
  return apiRequest(`/api/review/sessions/${sessionId}/decision`, {
    method: 'POST',
    body: { decision, note: note.trim() || null },
  })
}

export function addSessionNote(sessionId: string, text: string): Promise<ReviewNote> {
  return apiRequest<ReviewNote>(`/api/review/sessions/${sessionId}/notes`, { method: 'POST', body: { text } })
}

/** Olay silinmez; skordan çıkar. Geri alınamaz. */
export function markFalseAlarm(eventId: string): Promise<void> {
  return apiRequest(`/api/events/${eventId}/false-alarm`, { method: 'POST' })
}

/** `SessionReadyForReview` mesajı: aday adı yok, panel mesajı alınca listeyi yeniden çeker. */
export interface SessionReadyMessage {
  sessionId: string
  examId: string
  riskScore: number
  flags: SessionFlag[]
  endedAt?: string | null
}

export const REVIEW_HUB_PATH = '/hubs/review'
export const SESSION_READY_MESSAGE = 'SessionReadyForReview'

/** Belirtecin bitişine bu kadar kala (sn) bağlanmadan önce yenilenir. */
const TOKEN_REFRESH_MARGIN_SEC = 60

function expiresSoon(accessToken: string): boolean {
  try {
    const payload = JSON.parse(atob(accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: unknown }
    return typeof payload.exp === 'number' && payload.exp - Date.now() / 1000 < TOKEN_REFRESH_MARGIN_SEC
  } catch {
    return false
  }
}

async function currentAccessToken(): Promise<string> {
  const access = tokenStore.get()?.accessToken
  if (access && expiresSoon(access)) await refreshTokens()
  return tokenStore.get()?.accessToken ?? ''
}

/**
 * Bildirim bağlantısı. Tarayıcı WebSocket'te başlık gönderemediği için belirteç `access_token` sorgusuyla gider.
 * Sunucu belirteç dolunca bağlantıyı kapatır (K-31); otomatik yeniden bağlanma her denemede
 * `accessTokenFactory`'yi çağırdığı için süresi dolmak üzere olan belirteç önce yenilenir.
 */
export function createReviewConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(REVIEW_HUB_PATH, { accessTokenFactory: currentAccessToken })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()
}
