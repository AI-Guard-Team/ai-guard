import { apiRequest } from './client'

export type ExamType = 'GeneralAptitude' | 'EnglishProficiency'

/** Ortak adlar (CLAUDE.md); sunucudaki SessionState ile aynı yazım. */
export type SessionState =
  | 'ConsentPending'
  | 'AgentPairing'
  | 'IdentityVerification'
  | 'InProgress'
  | 'PermissionPending'
  | 'Disconnected'
  | 'Completed'
  | 'Cancelled'
  | 'Decided'

/** Adayın bu sınavdaki en son oturumu (K-36). */
export interface MySession {
  id: string
  state: SessionState
  startedAt?: string | null
  endedAt?: string | null
}

/** `GET /api/exams` satırı (E3 K-23 + K-36). Aday için `attemptUsed` ve `mySession` dolu. */
export interface ExamSummary {
  id: string
  name: string
  type: ExamType
  durationMinutes: number
  windowStartAt: string
  windowEndAt: string
  latestStartAt: string
  retentionDays: number
  questionCount: number
  attemptUsed?: boolean | null
  mySession?: MySession | null
}

export interface StartSessionResponse {
  sessionId: string
  state: SessionState
}

export const EXAM_TYPE_LABEL: Record<ExamType, string> = {
  GeneralAptitude: 'Genel yetenek',
  EnglishProficiency: 'İngilizce yeterlik',
}

export async function listExams(signal?: AbortSignal): Promise<ExamSummary[]> {
  return (await apiRequest<ExamSummary[]>('/api/exams', { signal })) ?? []
}

/**
 * Sınavı başlatır ya da adayın açık oturumunu döndürür (K-24: 201 yeni, 200 açık oturum).
 * 409'da sunucunun Türkçe nedeni `detail`'de (pencere açılmadı, son başlangıç geçti, hak kullanıldı).
 */
export function startSession(examId: string): Promise<StartSessionResponse | undefined> {
  return apiRequest<StartSessionResponse>(`/api/exams/${examId}/sessions`, { method: 'POST' })
}

/** Adaya yalnızca atandığı sınav döner; atanmadığı sınav 404 (K-23). */
export function getExam(examId: string, signal?: AbortSignal): Promise<ExamSummary | undefined> {
  return apiRequest<ExamSummary>(`/api/exams/${examId}`, { signal })
}
