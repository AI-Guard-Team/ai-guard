import type { ExamSummary, SessionState } from '../../api/exams'

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

const DATE_TIME = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })

export function formatDateTime(value: string | Date): string {
  return DATE_TIME.format(typeof value === 'string' ? new Date(value) : value)
}

/**
 * Bir sınav kartının durumu ve eylemi. Kurallar sunucudakiyle aynı (K-23, K-24, FR-10):
 * pencere açılmadan ve son başlangıçtan sonra yeni oturum yok; hak kullanıldıysa yeni oturum yok;
 * rıza reddiyle iptal edilen oturum hakkı tüketmez; kayıt fotoğrafı yoksa sınava girilemez (FR-04).
 * Sunucu yine de son sözü söyler: başlatma isteği 409 dönerse nedeni ekranda gösterilir.
 */
export function examStatus(exam: ExamSummary, hasReferencePhoto: boolean, now: Date): ExamStatus {
  const session = exam.mySession ?? null
  const windowStart = new Date(exam.windowStartAt)
  const latestStart = new Date(exam.latestStartAt)

  if (session && RUNNING.has(session.state)) {
    return {
      kind: 'running',
      label: 'Sınav sürüyor',
      tone: 'warning',
      note: session.state === 'Disconnected' ? 'Bağlantınız kesildi; dönünce kimliğiniz yeniden doğrulanır.' : 'Süreniz işliyor.',
      action: { kind: 'resume-exam', sessionId: session.id },
    }
  }

  if (session && FINISHED.has(session.state)) {
    return {
      kind: 'submitted',
      label: 'Tamamlandı',
      tone: 'success',
      note: session.endedAt ? `Teslim edildi · ${formatDateTime(session.endedAt)}` : 'Teslim edildi',
      action: { kind: 'none' },
    }
  }

  if (exam.attemptUsed) {
    // İptal edilmiş ama hakkı tüketmiş oturum: sınav sırasında biyometrik rıza geri alındı (FR-05).
    return { kind: 'used', label: 'Hak kullanıldı', tone: 'neutral', note: 'Bu sınav için oturum hakkınızı kullandınız.', action: { kind: 'none' } }
  }

  if (now < windowStart) {
    return {
      kind: 'upcoming',
      label: 'Yaklaşan',
      tone: 'info',
      note: `${formatDateTime(windowStart)} itibarıyla başlatılabilir`,
      action: { kind: 'none' },
    }
  }

  if (now > latestStart) {
    return {
      kind: 'closed',
      label: 'Süresi geçti',
      tone: 'neutral',
      note: 'Son başlangıç zamanı geçti; bu sınava artık girilemez.',
      action: { kind: 'none' },
    }
  }

  if (!hasReferencePhoto) {
    return {
      kind: 'needs-photo',
      label: 'Fotoğraf gerekli',
      tone: 'danger',
      note: 'Kayıt fotoğrafı yüklenmeden sınava başlanamaz.',
      action: { kind: 'upload-photo' },
    }
  }

  if (session && PREPARING.has(session.state)) {
    return {
      kind: 'preparing',
      label: 'Hazırlık sürüyor',
      tone: 'info',
      note: 'Sınav öncesi adımları tamamlamadınız. Süre kimlik doğrulamasından sonra başlar.',
      action: { kind: 'resume-preflight', sessionId: session.id },
    }
  }

  return {
    kind: 'ready',
    label: session?.state === 'Cancelled' ? 'Yeniden başlatılabilir' : 'Başlatılabilir',
    tone: 'success',
    // Son başlangıç kartın bilgi kutusunda zaten yazıyor; not yalnızca ek bilgi taşır.
    note: session?.state === 'Cancelled' ? 'Önceki oturum rıza verilmediği için iptal edildi; hakkınız duruyor.' : undefined,
    action: { kind: 'start' },
  }
}
