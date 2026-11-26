import { apiRequest } from './client'
import type { ExamType } from './exams'

// Yönetici uçları (E2, E3, A5; K-20, K-23, K-32). Ad ve alanlar sunucudaki modellerle birebir.

export type UserRole = 'Admin' | 'Reviewer' | 'Candidate'
export type InviteStatus = 'Pending' | 'Expired' | 'Accepted'
export type AssignmentRole = 'Candidate' | 'Reviewer'

export interface UserSummary {
  id: string
  fullName: string
  email: string
  role: UserRole
  isActive: boolean
  inviteStatus: InviteStatus
  createdAt: string
}

export interface Created {
  id: string
}

export function listUsers(role?: UserRole, signal?: AbortSignal): Promise<UserSummary[]> {
  return apiRequest<UserSummary[]>(`/api/users${role ? `?role=${role}` : ''}`, { signal }).then((rows) => rows ?? [])
}

/** Hesap açar ve davet e-postası gönderir (şifre belirleme bağlantısı, K-20). */
export function createUser(body: { fullName: string; email: string; role: UserRole }): Promise<Created> {
  return apiRequest<Created>('/api/users', { method: 'POST', body })
}

export function updateUser(id: string, body: { role?: UserRole; isActive?: boolean }): Promise<void> {
  return apiRequest(`/api/users/${id}`, { method: 'PATCH', body })
}

export interface ExamRequest {
  name: string
  type: ExamType
  durationMinutes: number
  windowStartAt: string
  windowEndAt: string
  retentionDays: number
}

export function createExam(body: ExamRequest): Promise<Created> {
  return apiRequest<Created>('/api/exams', { method: 'POST', body })
}

export function updateExam(id: string, body: ExamRequest): Promise<void> {
  return apiRequest(`/api/exams/${id}`, { method: 'PUT', body })
}

export function deleteExam(id: string): Promise<void> {
  return apiRequest(`/api/exams/${id}`, { method: 'DELETE' })
}

export interface QuestionDetail {
  id: string
  text: string
  options: string[]
  correctOptionIndex: number
  order: number
}

export interface QuestionRequest {
  text: string
  options: string[]
  correctOptionIndex: number
  order?: number
}

export function listQuestions(examId: string, signal?: AbortSignal): Promise<QuestionDetail[]> {
  return apiRequest<QuestionDetail[]>(`/api/exams/${examId}/questions`, { signal }).then((rows) => rows ?? [])
}

export function addQuestion(examId: string, body: QuestionRequest): Promise<Created> {
  return apiRequest<Created>(`/api/exams/${examId}/questions`, { method: 'POST', body })
}

export function updateQuestion(id: string, body: QuestionRequest): Promise<void> {
  return apiRequest(`/api/questions/${id}`, { method: 'PUT', body })
}

export function deleteQuestion(id: string): Promise<void> {
  return apiRequest(`/api/questions/${id}`, { method: 'DELETE' })
}

export interface AssignmentDetail {
  id: string
  userId: string
  fullName: string
  email: string
  role: AssignmentRole
  assignedAt: string
  attemptUsedAt: string | null
}

export function listAssignments(examId: string, signal?: AbortSignal): Promise<AssignmentDetail[]> {
  return apiRequest<AssignmentDetail[]>(`/api/exams/${examId}/assignments`, { signal }).then((rows) => rows ?? [])
}

export function addAssignment(examId: string, userId: string, role: AssignmentRole): Promise<Created> {
  return apiRequest<Created>(`/api/exams/${examId}/assignments`, { method: 'POST', body: { userId, role } })
}

export function deleteAssignment(id: string): Promise<void> {
  return apiRequest(`/api/assignments/${id}`, { method: 'DELETE' })
}

export interface AuditLogView {
  id: string
  occurredAt: string
  action: string
  actorUserId: string | null
  actorName: string | null
  targetType: string | null
  targetId: string | null
  oldValue: string | null
  newValue: string | null
}

export interface AuditLogPage {
  total: number
  page: number
  pageSize: number
  items: AuditLogView[]
}

export interface AuditQuery {
  action?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export function listAuditLogs(query: AuditQuery, signal?: AbortSignal): Promise<AuditLogPage> {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) if (value !== undefined && value !== '') params.set(key, String(value))
  return apiRequest<AuditLogPage>(`/api/audit-logs?${params}`, { signal })
}

export interface EventRiskRule {
  type: string
  weight: number
  decayPerMinute: number
  threshold: number | null
  maxTotal: number | null
}

export interface RiskSettings {
  sessionThreshold: number
  repetitionStep: number
  maxRepetitionMultiplier: number
  durationScaleSeconds: number
  events: EventRiskRule[]
}

/** `GET /api/risk-settings` (K-41): riskconfig.json'daki değerler, salt okunur. */
export function getRiskSettings(signal?: AbortSignal): Promise<RiskSettings> {
  return apiRequest<RiskSettings>('/api/risk-settings', { signal })
}
