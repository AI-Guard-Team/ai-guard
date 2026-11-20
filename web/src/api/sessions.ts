import { apiRequest } from './client'

export const SESSION_TEXT_VERSION = 'kvkk-session-v1'

export function submitSessionConsent(sessionId: string, granted: boolean): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/consent`, {
    method: 'POST',
    body: { granted, textVersion: SESSION_TEXT_VERSION },
  })
}

export function verifyIdentity(sessionId: string, frames: Blob[]): Promise<{ passed?: boolean } | undefined> {
  const body = new FormData()
  frames.forEach((frame, index) => body.append('frames', frame, `kare-${index + 1}.jpg`))
  return apiRequest(`/api/sessions/${sessionId}/verify`, { method: 'POST', body })
}

/** Gerekçe ExamSession.ContinueJustification alanına gider. Uç adı Y4 ile netleştirilecek. */
export function continueWithoutAgent(sessionId: string, justification: string): Promise<void> {
  return apiRequest(`/api/sessions/${sessionId}/continue-without-agent`, {
    method: 'POST',
    body: { justification },
  })
}
