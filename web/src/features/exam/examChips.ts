import type { AgentStatus } from '../../api/sessions'
import { defineMessages, pick } from '../../i18n/messages'

export type Tone = 'ok' | 'warn' | 'off'

export interface Chip {
  label: string
  value: string
  tone: Tone
}

const messages = defineMessages({
  tr: {
    camera: 'Kamera',
    agent: 'Ajan',
    connection: 'Bağlantı',
    noPermission: 'İzin yok',
    on: 'Açık',
    off: 'Kapalı',
    running: 'Çalışıyor',
    noSignal: 'Sinyal yok',
    none: 'Yok',
    disconnected: 'Kesik',
    connected: 'Bağlı',
  },
  en: {
    camera: 'Camera',
    agent: 'Agent',
    connection: 'Connection',
    noPermission: 'No permission',
    on: 'On',
    off: 'Off',
    running: 'Running',
    noSignal: 'No signal',
    none: 'None',
    disconnected: 'Lost',
    connected: 'Online',
  },
})

export function cameraChip(state: string | undefined, live: boolean, permission: PermissionState | null): Chip {
  const m = pick(messages)
  if (state === 'PermissionPending' || permission === 'denied') return { label: m.camera, value: m.noPermission, tone: 'warn' }
  if (live || permission === 'granted') return { label: m.camera, value: m.on, tone: 'ok' }
  return { label: m.camera, value: m.off, tone: 'off' }
}

export function agentChip(status: AgentStatus | null): Chip {
  const m = pick(messages)
  if (!status) return { label: m.agent, value: '…', tone: 'off' }
  if (status.agentActive) return { label: m.agent, value: m.running, tone: 'ok' }
  if (status.paired) return { label: m.agent, value: m.noSignal, tone: 'warn' }
  return { label: m.agent, value: m.none, tone: 'off' }
}

export function connectionChip(connected: boolean, state: string | undefined): Chip {
  const m = pick(messages)
  return !connected || state === 'Disconnected' ? { label: m.connection, value: m.disconnected, tone: 'warn' } : { label: m.connection, value: m.connected, tone: 'ok' }
}
