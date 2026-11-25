import { Clock3 } from 'lucide-react'
import { formatRemaining } from './remainingTime'

const WARNING_MS = 5 * 60_000
const DANGER_MS = 60_000

/** Kalan süre (sunucu saatine göre, K-24). Son 5 dakikada kehribar, son dakikada kırmızı. */
export function ExamTimer({ remaining }: { remaining: number | null }) {
  const tone = remaining === null ? 'idle' : remaining <= DANGER_MS ? 'danger' : remaining <= WARNING_MS ? 'warning' : 'normal'
  return (
    <div className={`exam-timer exam-timer-${tone}`} role="timer" aria-label="Kalan süre">
      <Clock3 size={20} aria-hidden="true" />
      <span className="exam-timer-value numeric">{remaining !== null ? formatRemaining(remaining) : '—:—'}</span>
    </div>
  )
}
