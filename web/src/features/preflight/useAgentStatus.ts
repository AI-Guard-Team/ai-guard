import { useCallback, useEffect, useRef, useState } from 'react'
import { AGENT_STATUS_POLL_MS, getAgentStatus, type AgentStatus } from '../../api/sessions'

/**
 * Ajan durumunu birkaç saniyede bir yoklar (K-25). Sunucu oturumu kimlik adımına geçirince `onReady` çağrılır.
 * Yoklama hatası adaya gösterilmez; bir sonraki yoklama düzeltir. Sekme arka plandayken yoklama durur.
 */
export function useAgentStatus(sessionId: string, onReady: () => void) {
  const [status, setStatus] = useState<AgentStatus | null>(null)
  const request = useRef(0)
  const ready = useRef(onReady)
  useEffect(() => {
    ready.current = onReady
  }, [onReady])

  const refresh = useCallback(async () => {
    const id = ++request.current
    let next: AgentStatus | undefined
    try {
      next = await getAgentStatus(sessionId)
    } catch {
      return
    }
    if (id !== request.current || !next) return
    setStatus(next)
    if (next.state === 'IdentityVerification') ready.current()
  }, [sessionId])

  useEffect(() => {
    void refresh()
    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') void refresh()
    }, AGENT_STATUS_POLL_MS)
    return () => {
      request.current += 1
      window.clearInterval(timer)
    }
  }, [refresh])

  return { status, refresh }
}
