import { tokenStore } from '../auth/tokenStore'
import { ApiError, apiRequest, refreshTokens } from './client'

// Değerlendirici ve rapor uçları (A4, K-31; A5, K-32). Ad ve alanlar sunucudaki ReviewModels ile birebir.

export type SessionFlag = 'Risky' | 'Indicated' | 'NoAgent' | 'MonitoringGap' | 'PermissionGap'
export type ReviewStatus = 'NotStarted' | 'AwaitingReview' | 'Completed' | 'Decided'
export type ReviewDecisionType = 'Clean' | 'Suspicious' | 'Violation'
export type ReviewSort = 'Priority' | 'Score' | 'StartedAt'
export type EventSource = 'Camera' | 'Audio' | 'Browser' | 'Device' | 'Network' | 'System'

export interface ReviewSessionRow {
  sessionId: string
  candidateId: string
  candidateName: string
  startedAt: string
  endedAt: string | null
  riskScore: number
  flags: SessionFlag[]
  status: ReviewStatus
  decision: ReviewDecisionType | null
  topReason: string | null
}

export interface ReviewSessionList {
  pendingCount: number
  sessions: ReviewSessionRow[]
}

export interface ReviewEvent {
  id: string
  type: string
  source: EventSource
  startedAt: string
  durationSec: number
  confidence: number | null
  reason: string | null
  contribution: number
  isFalseAlarm: boolean
  falseAlarmMarkedBy: string | null
  falseAlarmMarkedAt: string | null
  evidenceUrl: string | null
}

export interface ScorePoint {
  at: string
  score: number
}

export interface ReviewDecisionView {
  decision: ReviewDecisionType
  note: string | null
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
  startedAt: string | null
  endedAt: string | null
  riskScore: number
  flags: SessionFlag[]
  decision: ReviewDecisionView | null
  notes: ReviewNote[]
  events: ReviewEvent[]
  scoreHistory: ScorePoint[]
  monitoringDataDeletedAt: string | null
}

export interface ExamResultView {
  questionCount: number
  correct: number
  wrong: number
  blank: number
  unmonitored: number
}

export interface EventTypeSummary {
  type: string
  count: number
  falseAlarms: number
  contribution: number
}

export interface CandidateReport {
  sessionId: string
  examId: string
  examName: string
  examType: string
  durationMinutes: number
  candidateId: string
  candidateName: string
  candidateEmail: string
  state: string
  status: ReviewStatus
  createdAt: string
  startedAt: string | null
  endedAt: string | null
  result: ExamResultView | null
  riskScore: number
  flags: SessionFlag[]
  topReasons: string[]
  events: EventTypeSummary[]
  verificationAttempts: number
  decision: ReviewDecisionView | null
  notes: ReviewNote[]
  monitoringDataDeletedAt: string | null
}

export interface ReviewListFilter {
  risky?: boolean
  flags?: SessionFlag[]
  sort?: ReviewSort
}

export function getReviewSessions(examId: string, filter: ReviewListFilter = {}, signal?: AbortSignal): Promise<ReviewSessionList> {
  const query = new URLSearchParams()
  if (filter.risky !== undefined) query.set('risky', String(filter.risky))
  for (const flag of filter.flags ?? []) query.append('flags', flag)
  if (filter.sort) query.set('sort', filter.sort)
  const suffix = query.toString() ? `?${query}` : ''
  return apiRequest<ReviewSessionList>(`/api/review/exams/${examId}/sessions${suffix}`, { signal })
}

export function getReviewSession(sessionId: string, signal?: AbortSignal): Promise<ReviewSessionDetail> {
  return apiRequest<ReviewSessionDetail>(`/api/review/sessions/${sessionId}`, { signal })
}

export function decide(sessionId: string, decision: ReviewDecisionType, note: string | null): Promise<void> {
  return apiRequest(`/api/review/sessions/${sessionId}/decision`, { method: 'POST', body: { decision, note } })
}

export function addNote(sessionId: string, text: string): Promise<void> {
  return apiRequest(`/api/review/sessions/${sessionId}/notes`, { method: 'POST', body: { text } })
}

export function markFalseAlarm(eventId: string): Promise<void> {
  return apiRequest(`/api/events/${eventId}/false-alarm`, { method: 'POST' })
}

export function getCandidateReport(examId: string, sessionId: string, signal?: AbortSignal): Promise<CandidateReport> {
  return apiRequest<CandidateReport>(`/api/reports/exams/${examId}/sessions/${sessionId}`, { signal })
}

export type ReportFormat = 'Xlsx' | 'Pdf'

/**
 * Raporu dosya olarak indirir. Uç yetki istediği için düz bağlantı olamaz; belirteçle çekilip tarayıcıya
 * dosya olarak verilir. Sunucunun verdiği dosya adı korunur.
 */
export async function downloadCandidateReport(examId: string, sessionId: string, format: ReportFormat): Promise<void> {
  const path = `/api/reports/exams/${examId}/sessions/${sessionId}?format=${format}`
  const send = () => fetch(path, { headers: { Authorization: `Bearer ${tokenStore.get()?.accessToken ?? ''}` } })
  let response = await send().catch(() => {
    throw new ApiError(0, 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.')
  })
  if (response.status === 401 && (await refreshTokens())) response = await send()
  if (!response.ok) throw new ApiError(response.status, 'Rapor indirilemedi. Lütfen tekrar deneyin.')

  const disposition = response.headers.get('Content-Disposition') ?? ''
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
  const fileName = match ? decodeURIComponent(match[1]) : `aday-raporu.${format === 'Pdf' ? 'pdf' : 'xlsx'}`
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
