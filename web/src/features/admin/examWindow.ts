import type { ExamSummary } from '../../api/exams'
import type { BadgeTone } from '../../components/StatusBadge'
import { defineMessages, live } from '../../i18n/messages'

export type WindowPhase = 'upcoming' | 'open' | 'closing' | 'closed'

/**
 * Sınavın erişim penceresine göre evresi (K-23, FR-06). "closing": pencere açık ama son başlangıç geçti; yeni aday
 * başlayamaz, başlamış olanlar sürüyor.
 */
export function windowPhase(exam: Pick<ExamSummary, 'windowStartAt' | 'windowEndAt' | 'latestStartAt'>, now: number): WindowPhase {
  const start = new Date(exam.windowStartAt).getTime()
  const end = new Date(exam.windowEndAt).getTime()
  const latest = new Date(exam.latestStartAt).getTime()
  if (now < start) return 'upcoming'
  if (now >= end) return 'closed'
  return now > latest ? 'closing' : 'open'
}

export const PHASE_LABEL: Record<WindowPhase, string> = live(
  defineMessages<Record<WindowPhase, string>>({
    tr: { upcoming: 'Planlandı', open: 'Erişime açık', closing: 'Son başlangıç geçti', closed: 'Sona erdi' },
    en: { upcoming: 'Scheduled', open: 'Open', closing: 'Latest start passed', closed: 'Ended' },
  }),
)

export const PHASE_TONE: Record<WindowPhase, BadgeTone> = {
  upcoming: 'info',
  open: 'success',
  closing: 'warning',
  closed: 'neutral',
}
