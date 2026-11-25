import type { BadgeTone } from '../components/StatusBadge'
import type { DecisionType, ReviewSort, ReviewStatus, SessionFlag } from '../api/review'

// Her işaret listede ayrı rozet olarak görünür (FR-27).
export const FLAG_LABEL: Record<SessionFlag, string> = {
  Risky: 'Riskli',
  NoAgent: 'Ajansız',
  Indicated: 'Göstergeli',
  PermissionGap: 'İzinsiz süre',
  MonitoringGap: 'İzleme kesintisi',
}

export const FLAG_TONE: Record<SessionFlag, BadgeTone> = {
  Risky: 'danger',
  NoAgent: 'warning',
  Indicated: 'warning',
  PermissionGap: 'info',
  MonitoringGap: 'info',
}

export const FLAGS: SessionFlag[] = ['Risky', 'NoAgent', 'Indicated', 'PermissionGap', 'MonitoringGap']

export const STATUS_LABEL: Record<ReviewStatus, string> = {
  AwaitingReview: 'Karar bekliyor',
  Completed: 'Tamamlandı',
  Decided: 'Karar verildi',
  NotStarted: 'Başlamadı',
}

export const STATUS_TONE: Record<ReviewStatus, BadgeTone> = {
  AwaitingReview: 'warning',
  Completed: 'neutral',
  Decided: 'success',
  NotStarted: 'neutral',
}

export const DECISION_LABEL: Record<DecisionType, string> = {
  Clean: 'Temiz',
  Suspicious: 'Şüpheli',
  Violation: 'İhlal',
}

export const DECISION_TONE: Record<DecisionType, BadgeTone> = {
  Clean: 'success',
  Suspicious: 'warning',
  Violation: 'danger',
}

export const DECISIONS: DecisionType[] = ['Clean', 'Suspicious', 'Violation']

export const SORT_LABEL: Record<ReviewSort, string> = {
  Priority: 'Öncelik (karar bekleyenler önde)',
  Score: 'Risk skoru',
  StartedAt: 'Başlangıç zamanı',
}

const EVENT_LABEL: Record<string, string> = {
  FACE_MISSING: 'Yüz görünmüyor',
  MULTIPLE_FACES: 'Birden fazla yüz',
  GAZE_AWAY: 'Bakış ekran dışında',
  PHONE: 'Telefon',
  IDENTITY_MISMATCH: 'Kimlik uyuşmazlığı',
  VERIFICATION_FAILED: 'Doğrulama başarısız',
  SPEECH: 'Konuşma',
  TAB_BLUR: 'Sekme değiştirildi',
  FULLSCREEN_EXIT: 'Tam ekrandan çıkıldı',
  COPY_PASTE: 'Kopyala/yapıştır',
  RIGHT_CLICK: 'Sağ tık',
  PERMISSION_REVOKED: 'İzin kaldırıldı',
  VM_DETECTED: 'Sanal makine',
  HYPERVISOR_INFO: 'Hipervizör bilgisi',
  FORBIDDEN_APP: 'Yasaklı uygulama',
  VPN_PROXY: 'VPN / vekil sunucu',
  IP_CHANGE: 'IP değişti',
  AGENT_SIGNAL_LOST: 'Ajan sinyali kayboldu',
  AGENT_SIGNAL_INVALID: 'Ajan sinyali geçersiz',
  CONNECTION_LOST: 'Bağlantı kesildi',
  MONITORING_GAP: 'İzleme kesintisi',
}

const SOURCE_LABEL: Record<string, string> = {
  Camera: 'Kamera',
  Audio: 'Ses',
  Browser: 'Tarayıcı',
  Device: 'Cihaz',
  Network: 'Ağ',
  System: 'Sistem',
}

export function eventLabel(type: string): string {
  return EVENT_LABEL[type] ?? type
}

export function sourceLabel(source: string): string {
  return SOURCE_LABEL[source] ?? source
}

const DATE_TIME = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })
const TIME = new Intl.DateTimeFormat('tr-TR', { timeStyle: 'medium' })

export function formatDateTime(value: string | null | undefined): string {
  return value ? DATE_TIME.format(new Date(value)) : '—'
}

export function formatTime(value: string | number): string {
  return TIME.format(new Date(value))
}

export function formatScore(score: number): string {
  return score.toFixed(1)
}

export function formatDuration(seconds: number): string {
  if (seconds < 1) return '<1 sn'
  if (seconds < 60) return `${Math.round(seconds)} sn`
  return `${Math.floor(seconds / 60)} dk ${Math.round(seconds % 60)} sn`
}
