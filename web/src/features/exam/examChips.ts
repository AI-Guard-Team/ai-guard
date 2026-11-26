import type { AgentStatus } from '../../api/sessions'

export type Tone = 'ok' | 'warn' | 'off'

export interface Chip {
  label: string
  value: string
  tone: Tone
}

export function cameraChip(state: string | undefined, live: boolean, permission: PermissionState | null): Chip {
  if (state === 'PermissionPending' || permission === 'denied') return { label: 'Kamera', value: 'İzin yok', tone: 'warn' }
  if (live || permission === 'granted') return { label: 'Kamera', value: 'Açık', tone: 'ok' }
  return { label: 'Kamera', value: 'Kapalı', tone: 'off' }
}

export function agentChip(status: AgentStatus | null): Chip {
  if (!status) return { label: 'Ajan', value: '…', tone: 'off' }
  if (status.agentActive) return { label: 'Ajan', value: 'Çalışıyor', tone: 'ok' }
  if (status.paired) return { label: 'Ajan', value: 'Sinyal yok', tone: 'warn' }
  return { label: 'Ajan', value: 'Yok', tone: 'off' }
}

export function connectionChip(connected: boolean, state: string | undefined): Chip {
  return !connected || state === 'Disconnected' ? { label: 'Bağlantı', value: 'Kesik', tone: 'warn' } : { label: 'Bağlantı', value: 'Bağlı', tone: 'ok' }
}
