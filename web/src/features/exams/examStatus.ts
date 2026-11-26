import type { ExamSummary, SessionState } from '../../api/exams'
import { defineMessages, pick } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'

export { formatDateTime }

const messages = defineMessages({
  tr: {
    running: 'Sınav sürüyor',
    disconnected: 'Bağlantınız kesildi; dönünce kimliğiniz yeniden doğrulanır.',
    timeRunning: 'Süreniz işliyor.',
    completed: 'Tamamlandı',
    submittedAt: (at: string) => `Teslim edildi · ${at}`,
    submitted: 'Teslim edildi',
    used: 'Hak kullanıldı',
    usedNote: 'Bu sınav için oturum hakkınızı kullandınız.',
    upcoming: 'Yaklaşan',
    upcomingNote: (at: string) => `${at} itibarıyla başlatılabilir`,
    closed: 'Süresi geçti',
    closedNote: 'Son başlangıç zamanı geçti; bu sınava artık girilemez.',
    needsPhoto: 'Fotoğraf gerekli',
    needsPhotoNote: 'Kayıt fotoğrafı yüklenmeden sınava başlanamaz.',
    preparing: 'Hazırlık sürüyor',
    preparingNote: 'Sınav öncesi adımları tamamlamadınız. Süre kimlik doğrulamasından sonra başlar.',
    restartable: 'Yeniden başlatılabilir',
    ready: 'Başlatılabilir',
    cancelledNote: 'Önceki oturum rıza verilmediği için iptal edildi; hakkınız duruyor.',
  },
  en: {
    running: 'In progress',
    disconnected: 'Your connection dropped; when you return your identity is verified again.',
    timeRunning: 'Your time is running.',
    completed: 'Completed',
    submittedAt: (at: string) => `Submitted · ${at}`,
    submitted: 'Submitted',
    used: 'Attempt used',
    usedNote: 'You have used your attempt for this exam.',
    upcoming: 'Upcoming',
    upcomingNote: (at: string) => `Can be started from ${at}`,
    closed: 'Closed',
    closedNote: 'The latest start time has passed; this exam can no longer be taken.',
    needsPhoto: 'Photo required',
    needsPhotoNote: 'You cannot start the exam before uploading a reference photo.',
    preparing: 'Preparation in progress',
    preparingNote: 'You have not finished the pre-exam steps. The timer starts after identity verification.',
    restartable: 'Can be restarted',
    ready: 'Ready to start',
    cancelledNote: 'The previous session was cancelled because consent was not given; your attempt is still available.',
  },
})

export type ExamStatusKind =
  | 'upcoming'
  | 'ready'
  | 'needs-photo'
  | 'preparing'
  | 'running'
  | 'submitted'
  | 'closed'
  | 'used'

export type ExamAction =
  | { kind: 'start' }
  | { kind: 'resume-preflight'; sessionId: string }
  | { kind: 'resume-exam'; sessionId: string }
  | { kind: 'upload-photo' }
  | { kind: 'none' }

export interface ExamStatus {
  kind: ExamStatusKind
  /** Kartın sağ üstündeki kısa durum. */
  label: string
  tone: 'success' | 'warning' | 'danger' | 'info' | 'neutral'
  /** Düğmenin altında ya da yerine gösterilen açıklama. */
  note?: string
  action: ExamAction
}

const PREPARING: ReadonlySet<SessionState> = new Set(['ConsentPending', 'AgentPairing', 'IdentityVerification'])
const RUNNING: ReadonlySet<SessionState> = new Set(['InProgress', 'PermissionPending', 'Disconnected'])
const FINISHED: ReadonlySet<SessionState> = new Set(['Completed', 'Decided'])


/**
 * Bir sınav kartının durumu ve eylemi. Kurallar sunucudakiyle aynı (K-23, K-24, FR-10):
 * pencere açılmadan ve son başlangıçtan sonra yeni oturum yok; hak kullanıldıysa yeni oturum yok;
 * rıza reddiyle iptal edilen oturum hakkı tüketmez; kayıt fotoğrafı yoksa sınava girilemez (FR-04).
 * Sunucu yine de son sözü söyler: başlatma isteği 409 dönerse nedeni ekranda gösterilir.
 */
export function examStatus(exam: ExamSummary, hasReferencePhoto: boolean, now: Date): ExamStatus {
  const m = pick(messages)
  const session = exam.mySession ?? null
  const windowStart = new Date(exam.windowStartAt)
  const latestStart = new Date(exam.latestStartAt)

  if (session && RUNNING.has(session.state)) {
    return {
      kind: 'running',
      label: m.running,
      tone: 'warning',
      note: session.state === 'Disconnected' ? m.disconnected : m.timeRunning,
      action: { kind: 'resume-exam', sessionId: session.id },
    }
  }

  if (session && FINISHED.has(session.state)) {
    return {
      kind: 'submitted',
      label: m.completed,
      tone: 'success',
      note: session.endedAt ? m.submittedAt(formatDateTime(session.endedAt)) : m.submitted,
      action: { kind: 'none' },
    }
  }

  if (exam.attemptUsed) {
    // İptal edilmiş ama hakkı tüketmiş oturum: sınav sırasında biyometrik rıza geri alındı (FR-05).
    return { kind: 'used', label: m.used, tone: 'neutral', note: m.usedNote, action: { kind: 'none' } }
  }

  if (now < windowStart) {
    return {
      kind: 'upcoming',
      label: m.upcoming,
      tone: 'info',
      note: m.upcomingNote(formatDateTime(windowStart)),
      action: { kind: 'none' },
    }
  }

  if (now > latestStart) {
    return {
      kind: 'closed',
      label: m.closed,
      tone: 'neutral',
      note: m.closedNote,
      action: { kind: 'none' },
    }
  }

  if (!hasReferencePhoto) {
    return {
      kind: 'needs-photo',
      label: m.needsPhoto,
      tone: 'danger',
      note: m.needsPhotoNote,
      action: { kind: 'upload-photo' },
    }
  }

  if (session && PREPARING.has(session.state)) {
    return {
      kind: 'preparing',
      label: m.preparing,
      tone: 'info',
      note: m.preparingNote,
      action: { kind: 'resume-preflight', sessionId: session.id },
    }
  }

  return {
    kind: 'ready',
    label: session?.state === 'Cancelled' ? m.restartable : m.ready,
    tone: 'success',
    // Son başlangıç kartın bilgi kutusunda zaten yazıyor; not yalnızca ek bilgi taşır.
    note: session?.state === 'Cancelled' ? m.cancelledNote : undefined,
    action: { kind: 'start' },
  }
}
