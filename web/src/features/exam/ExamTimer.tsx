import { defineMessages, useMessages } from '../../i18n/messages'
import { formatRemaining } from './remainingTime'

const messages = defineMessages({ tr: { remaining: 'Kalan süre' }, en: { remaining: 'Time remaining' } })

const WARNING_MS = 5 * 60_000
const DANGER_MS = 60_000

type TimerTone = 'idle' | 'normal' | 'warning' | 'danger'

function timerTone(remaining: number | null): TimerTone {
  if (remaining === null) return 'idle'
  return remaining <= DANGER_MS ? 'danger' : remaining <= WARNING_MS ? 'warning' : 'normal'
}

/** Kalan süre (sunucu saatine göre, K-24): büyük ama sakin mono rakam. Son 5 dakikada kehribar, son dakikada kırmızı. */
export function ExamTimer({ remaining }: { remaining: number | null }) {
  const m = useMessages(messages)
  return (
    <div className={`exam-timer exam-timer-${timerTone(remaining)}`} role="timer" aria-label={m.remaining}>
      <span className="exam-timer-value numeric">{remaining !== null ? formatRemaining(remaining) : '—:—'}</span>
    </div>
  )
}

/** Ekranın en üstünde 3px süre çizgisi; sağdan sola erir. Yalnızca görsel; süre `ExamTimer`'da okunur. */
export function ExamTimeLine({ remaining, totalMs }: { remaining: number | null; totalMs: number }) {
  const ratio = remaining === null || totalMs <= 0 ? 1 : Math.min(1, Math.max(0, remaining / totalMs))
  return (
    <div className={`exam-timeline exam-timeline-${timerTone(remaining)}`} aria-hidden="true">
      <span style={{ transform: `scaleX(${ratio})` }} />
    </div>
  )
}
