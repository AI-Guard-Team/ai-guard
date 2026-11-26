import { apiRequest } from './client'

// Yönetim uçları (K-49). Ad ve alanlar sunucudaki modellerle birebir.

// ---- Platform ayarları ----

export interface PlatformSettings {
  alternativeMethodText: string | null
  supportEmail: string | null
  supportPhone: string | null
  agentDownloadUrlWindows: string | null
  agentDownloadUrlMac: string | null
  updatedAt: string | null
}

export type PlatformSettingsRequest = Omit<PlatformSettings, 'updatedAt'>

/** Giriş yapmış herkes okur: aday alternatif yöntem ekranında ve ajan kurulumunda kullanıyor. */
export function getPlatformSettings(signal?: AbortSignal): Promise<PlatformSettings> {
  return apiRequest<PlatformSettings>('/api/platform-settings', { signal })
}

export function updatePlatformSettings(body: PlatformSettingsRequest): Promise<void> {
  return apiRequest('/api/platform-settings', { method: 'PUT', body })
}

// ---- Kullanıcı daveti ----

/** Yeni bağlantı üretir, eskisini geçersiz kılar (dakikada bir; kabul edilmiş davette 409). */
export function resendInvite(userId: string): Promise<void> {
  return apiRequest(`/api/users/${userId}/invite`, { method: 'POST' })
}

// ---- Sınava özel risk ayarı ----

export interface ExamRiskRule {
  type: string
  weight: number
  decayPerMinute: number
  threshold: number | null
  defaultWeight: number
  defaultDecayPerMinute: number
  defaultThreshold: number | null
  customized: boolean
}

export interface ExamRiskSettings {
  examId: string
  sessionThreshold: number
  defaultSessionThreshold: number
  /** Sınavda oturum açıldıysa ayar değiştirilemez (skorlar adaylar arasında tutarlı kalsın). */
  locked: boolean
  events: ExamRiskRule[]
}

export interface ExamRiskRuleRequest {
  type: string
  weight: number
  decayPerMinute: number
  threshold: number | null
}

export interface ExamRiskSettingsRequest {
  /** Boşsa platform eşiği. */
  sessionThreshold: number | null
  /** Yalnızca varsayılandan farklı türler; listede olmayan tür varsayılana döner. */
  events: ExamRiskRuleRequest[]
}

export function getExamRiskSettings(examId: string, signal?: AbortSignal): Promise<ExamRiskSettings> {
  return apiRequest<ExamRiskSettings>(`/api/exams/${examId}/risk-settings`, { signal })
}

export function updateExamRiskSettings(examId: string, body: ExamRiskSettingsRequest): Promise<void> {
  return apiRequest(`/api/exams/${examId}/risk-settings`, { method: 'PUT', body })
}

export function resetExamRiskSettings(examId: string): Promise<void> {
  return apiRequest(`/api/exams/${examId}/risk-settings`, { method: 'DELETE' })
}

// ---- Sınav düzeyi rapor ----

export interface ExamSummaryReport {
  examId: string
  examName: string
  durationMinutes: number
  assigned: number
  started: number
  inProgress: number
  completed: number
  awaitingReview: number
  decided: number
  risky: number
  averageRisk: number
  maxRisk: number
  averageCorrect: number
  questionCount: number
  decisions: { clean: number; suspicious: number; violation: number }
  events: { type: string; count: number; falseAlarms: number }[]
  density: { minute: number; events: number }[]
}

export function getExamSummary(examId: string, signal?: AbortSignal): Promise<ExamSummaryReport> {
  return apiRequest<ExamSummaryReport>(`/api/reports/exams/${examId}`, { signal })
}

// ---- Bildirimler ----

export type NotificationKind =
  | 'SessionAwaitingReview'
  | 'ExamAwaitingReview'
  | 'ExamWithoutQuestions'
  | 'ExamWithoutCandidates'
  | 'InvitesExpired'
  | 'ExamOpensSoon'
  | 'ExamOpenNow'
  | 'PhotoMissing'

export interface NotificationItem {
  /** Kararlı: aynı durum her istekte aynı kimlikle gelir; "okundu" bununla tutuluyor. */
  id: string
  kind: NotificationKind
  at: string
  examId: string | null
  examName: string | null
  sessionId: string | null
  candidateName: string | null
  count: number | null
}

export function listNotifications(signal?: AbortSignal): Promise<NotificationItem[]> {
  return apiRequest<NotificationItem[]>('/api/notifications', { signal }).then((rows) => rows ?? [])
}
