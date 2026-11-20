import { apiRequest } from './client'

export const SESSION_TEXT_VERSION = 'kvkk-session-v1'

export function submitSessionConsent(sessionId: string, granted: boolean): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/consent`, {
    method: 'POST',
    body: { accepted: granted, textVersion: SESSION_TEXT_VERSION },
  })
}

export function verifyIdentity(sessionId: string, frames: Blob[]): Promise<{ passed?: boolean } | undefined> {
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
