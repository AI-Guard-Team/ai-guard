import type { EventSource, ReviewDecisionType, ReviewStatus, SessionFlag } from '../api/review'
import type { BadgeTone } from '../components/StatusBadge'

// Olay, işaret, durum ve karar adlarının ekrandaki karşılıkları tek yerde. Anahtarlar sunucudaki ortak adlar
// (CLAUDE.md "Ortak adlar"); değiştirilmez, yalnızca Türkçe karşılıkları burada.

export const EVENT_LABEL: Record<string, string> = {
  FACE_MISSING: 'Yüz görünmüyor',
  MULTIPLE_FACES: 'Birden fazla yüz',
  GAZE_AWAY: 'Bakış ekrandan uzakta',
  PHONE: 'Telefon',
  IDENTITY_MISMATCH: 'Kimlik eşleşmedi',
  VERIFICATION_FAILED: 'Doğrulama başarısız',
  SPEECH: 'Konuşma',
  TAB_BLUR: 'Sekmeden çıkış',
  FULLSCREEN_EXIT: 'Tam ekrandan çıkış',
  COPY_PASTE: 'Kopyala/yapıştır',
  RIGHT_CLICK: 'Sağ tık',
  PERMISSION_REVOKED: 'İzin kaldırıldı',
  VM_DETECTED: 'Sanal makine',
  HYPERVISOR_INFO: 'Hipervizör bilgisi',
  FORBIDDEN_APP: 'Yasaklı uygulama',
  VPN_PROXY: 'VPN/vekil sunucu',
  IP_CHANGE: 'IP değişimi',
  AGENT_SIGNAL_LOST: 'Ajan sinyali kayboldu',
  AGENT_SIGNAL_INVALID: 'Ajan sinyali geçersiz',
  CONNECTION_LOST: 'Bağlantı koptu',
  MONITORING_GAP: 'İzleme boşluğu',
}

export const eventLabel = (type: string) => EVENT_LABEL[type] ?? type

/** Sinyal şeridindeki sıra: Kamera, Ses, Tarayıcı, Cihaz, Ağ, Sistem. */
export const SOURCES: readonly EventSource[] = ['Camera', 'Audio', 'Browser', 'Device', 'Network', 'System']

export const SOURCE_LABEL: Record<EventSource, string> = {
  Camera: 'Kamera',
  Audio: 'Ses',
  Browser: 'Tarayıcı',
  Device: 'Cihaz',
  Network: 'Ağ',
  System: 'Sistem',
}

export const FLAG_LABEL: Record<SessionFlag, string> = {
  Risky: 'Riskli',
  Indicated: 'Göstergeli',
  NoAgent: 'Ajansız',
  MonitoringGap: 'İzleme boşluğu',
  PermissionGap: 'İzin boşluğu',
}

export const FLAG_TONE: Record<SessionFlag, BadgeTone> = {
  Risky: 'danger',
  Indicated: 'warning',
  NoAgent: 'warning',
  MonitoringGap: 'neutral',
  PermissionGap: 'neutral',
}

export const STATUS_LABEL: Record<ReviewStatus, string> = {
  NotStarted: 'Başlamadı',
  AwaitingReview: 'Karar bekliyor',
  Completed: 'Tamamlandı',
  Decided: 'Karara bağlandı',
}

export const STATUS_TONE: Record<ReviewStatus, BadgeTone> = {
  NotStarted: 'neutral',
  AwaitingReview: 'warning',
  Completed: 'success',
  Decided: 'info',
}

export const DECISION_LABEL: Record<ReviewDecisionType, string> = {
  Clean: 'Temiz',
  Suspicious: 'Şüpheli',
  Violation: 'İhlal',
}

export const DECISION_TONE: Record<ReviewDecisionType, 'success' | 'warning' | 'danger'> = {
  Clean: 'success',
  Suspicious: 'warning',
  Violation: 'danger',
}
