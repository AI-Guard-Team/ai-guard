import type { EventSource, ReviewDecisionType, ReviewStatus, SessionFlag } from '../api/review'
import type { BadgeTone } from '../components/StatusBadge'
import { defineMessages, live } from '../i18n/messages'

// Olay, işaret, durum ve karar adlarının ekrandaki karşılıkları tek yerde. Anahtarlar sunucudaki ortak adlar
// (CLAUDE.md "Ortak adlar"); değiştirilmez, yalnızca ekrandaki karşılıkları burada (Türkçe ve İngilizce, K-46).

export const EVENT_LABEL: Record<string, string> = live(
  defineMessages<Record<string, string>>({
    tr: {
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
    },
    en: {
      FACE_MISSING: 'Face not visible',
      MULTIPLE_FACES: 'Multiple faces',
      GAZE_AWAY: 'Looking away from screen',
      PHONE: 'Phone',
      IDENTITY_MISMATCH: 'Identity mismatch',
      VERIFICATION_FAILED: 'Verification failed',
      SPEECH: 'Speech',
      TAB_BLUR: 'Left the tab',
      FULLSCREEN_EXIT: 'Left full screen',
      COPY_PASTE: 'Copy/paste',
      RIGHT_CLICK: 'Right click',
      PERMISSION_REVOKED: 'Permission revoked',
      VM_DETECTED: 'Virtual machine',
      HYPERVISOR_INFO: 'Hypervisor info',
      FORBIDDEN_APP: 'Forbidden app',
      VPN_PROXY: 'VPN/proxy',
      IP_CHANGE: 'IP change',
      AGENT_SIGNAL_LOST: 'Agent signal lost',
      AGENT_SIGNAL_INVALID: 'Agent signal invalid',
      CONNECTION_LOST: 'Connection lost',
      MONITORING_GAP: 'Monitoring gap',
    },
  }),
)

export const eventLabel = (type: string) => EVENT_LABEL[type] ?? type

/** Sinyal şeridindeki sıra: Kamera, Ses, Tarayıcı, Cihaz, Ağ, Sistem. */
export const SOURCES: readonly EventSource[] = ['Camera', 'Audio', 'Browser', 'Device', 'Network', 'System']

export const SOURCE_LABEL: Record<EventSource, string> = live(
  defineMessages<Record<EventSource, string>>({
    tr: { Camera: 'Kamera', Audio: 'Ses', Browser: 'Tarayıcı', Device: 'Cihaz', Network: 'Ağ', System: 'Sistem' },
    en: { Camera: 'Camera', Audio: 'Audio', Browser: 'Browser', Device: 'Device', Network: 'Network', System: 'System' },
  }),
)

export const FLAG_LABEL: Record<SessionFlag, string> = live(
  defineMessages<Record<SessionFlag, string>>({
    tr: { Risky: 'Riskli', Indicated: 'Göstergeli', NoAgent: 'Ajansız', MonitoringGap: 'İzleme boşluğu', PermissionGap: 'İzin boşluğu' },
    en: { Risky: 'Risky', Indicated: 'Indicators', NoAgent: 'No agent', MonitoringGap: 'Monitoring gap', PermissionGap: 'Permission gap' },
  }),
)

export const FLAG_TONE: Record<SessionFlag, BadgeTone> = {
  Risky: 'danger',
  Indicated: 'warning',
  NoAgent: 'warning',
  MonitoringGap: 'neutral',
  PermissionGap: 'neutral',
}

export const STATUS_LABEL: Record<ReviewStatus, string> = live(
  defineMessages<Record<ReviewStatus, string>>({
    tr: { NotStarted: 'Başlamadı', AwaitingReview: 'Karar bekliyor', Completed: 'Tamamlandı', Decided: 'Karara bağlandı' },
    en: { NotStarted: 'Not started', AwaitingReview: 'Awaiting decision', Completed: 'Completed', Decided: 'Decided' },
  }),
)

export const STATUS_TONE: Record<ReviewStatus, BadgeTone> = {
  NotStarted: 'neutral',
  AwaitingReview: 'warning',
  Completed: 'success',
  Decided: 'info',
}

export const DECISION_LABEL: Record<ReviewDecisionType, string> = live(
  defineMessages<Record<ReviewDecisionType, string>>({
    tr: { Clean: 'Temiz', Suspicious: 'Şüpheli', Violation: 'İhlal' },
    en: { Clean: 'Clean', Suspicious: 'Suspicious', Violation: 'Violation' },
  }),
)

export const DECISION_TONE: Record<ReviewDecisionType, 'success' | 'warning' | 'danger'> = {
  Clean: 'success',
  Suspicious: 'warning',
  Violation: 'danger',
}
