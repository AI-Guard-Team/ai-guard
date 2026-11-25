import type { ReviewEvent } from '../../api/review'
import { Button } from '../../components/Button'
import { eventLabel } from '../../lib/eventCatalog'
import { formatNumber, formatTimeSeconds } from '../../lib/format'

interface ContributionWaterfallProps {
  events: ReviewEvent[]
  riskScore: number
  /** Yanlış alarm işaretleyebilir mi (atanmış değerlendirici, karar verilmemiş). */
  canMark: boolean
  pendingId: string | null
  selectedId: string | null
  onMark: (event: ReviewEvent) => void
  onSelect: (event: ReviewEvent) => void
}

/**
 * "Skor neden X?" katkı şelalesi (Prompt F). Her çubuk bir olayın sınav bitişindeki katkısı (sunucunun hesabı:
 * azalma, tekrar ve tür üst sınırı dahil). Yanlış alarm işaretlenen olay taralı ve skora katılmıyor (K-31).
 */
export function ContributionWaterfall({ events, riskScore, canMark, pendingId, selectedId, onMark, onSelect }: ContributionWaterfallProps) {
  const scored = events.filter((item) => item.contribution > 0).sort((a, b) => b.contribution - a.contribution)
  const falseAlarms = events.filter((item) => item.isFalseAlarm)
  const total = scored.reduce((sum, item) => sum + item.contribution, 0)
  const scale = Math.max(total, 1)

  if (scored.length === 0 && falseAlarms.length === 0) {
    return <p className="waterfall-empty">Skora katkı yapan olay yok.</p>
  }

  // Şelale: her çubuk öncekilerin bittiği yerden başlar.
  const offsets = scored.map((_, index) => scored.slice(0, index).reduce((sum, item) => sum + item.contribution, 0))
  return (
    <div className="waterfall">
      <ol className="waterfall-list">
        {scored.map((item, index) => {
          const left = (offsets[index] / scale) * 100
          const width = (item.contribution / scale) * 100
          return (
            <li key={item.id} className={['waterfall-row', item.id === selectedId && 'is-selected'].filter(Boolean).join(' ')}>
              <button type="button" className="waterfall-label" onClick={() => onSelect(item)}>
                <span>{eventLabel(item.type)}</span>
                <span className="kicker numeric">{formatTimeSeconds(item.startedAt)}</span>
              </button>
              <span className="waterfall-track" aria-hidden="true">
                <span className="waterfall-bar" style={{ left: `${left}%`, width: `${Math.max(width, 0.8)}%` }} />
              </span>
              <span className="waterfall-value numeric">+{formatNumber(item.contribution, 1)}</span>
              {canMark ? (
                <Button variant="ghost" loading={pendingId === item.id} onClick={() => onMark(item)}>
                  Yanlış alarm
                </Button>
              ) : (
                <span />
              )}
            </li>
          )
        })}
        {falseAlarms.map((item) => (
          <li key={item.id} className="waterfall-row is-false-alarm">
            <button type="button" className="waterfall-label" onClick={() => onSelect(item)}>
              <span>{eventLabel(item.type)}</span>
              <span className="kicker numeric">{formatTimeSeconds(item.startedAt)}</span>
            </button>
            <span className="waterfall-track" aria-hidden="true">
              <span className="waterfall-bar" style={{ left: 0, width: '100%' }} />
            </span>
            <span className="waterfall-value numeric">0</span>
            <span className="waterfall-note">
              Yanlış alarm{item.falseAlarmMarkedBy ? ` · ${item.falseAlarmMarkedBy}` : ''}
            </span>
          </li>
        ))}
      </ol>
      <p className="waterfall-total">
        <span className="kicker">Oturum skoru</span>
        <span className="numeric">{formatNumber(riskScore)}</span>
      </p>
    </div>
  )
}
