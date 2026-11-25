import { useState } from 'react'
import type { ReviewEvent } from '../../api/review'
import { eventLabel } from '../../lib/eventCatalog'
import { formatDuration, formatNumber } from '../../lib/format'

interface ContributionBarsProps {
  events: ReviewEvent[]
  /** Bir çubuğa tıklanınca o türün en büyük katkılı olayı seçilir. */
  onSelect: (event: ReviewEvent) => void
  selectedType: string | null
}

interface TypeRow {
  type: string
  contribution: number
  count: number
  seconds: number
  falseAlarms: number
  top: ReviewEvent
}

/**
 * "Skor neden X?" (dataviz: büyüklük karşılaştırması → sıralı yatay çubuk, tek renk, tek seri). Her çubuk bir olay
 * türünün skora toplam katkısı (sunucunun hesabı; yanlış alarmlar 0). Değer çubuğun ucunda, metin renginde;
 * üzerine gelince ayrıntı.
 */
export function ContributionBars({ events, onSelect, selectedType }: ContributionBarsProps) {
  const [hover, setHover] = useState<string | null>(null)
  const byType = new Map<string, TypeRow>()
  for (const event of events) {
    const row = byType.get(event.type) ?? { type: event.type, contribution: 0, count: 0, seconds: 0, falseAlarms: 0, top: event }
    row.contribution += event.contribution
    row.count += 1
    row.seconds += event.durationSec
    if (event.isFalseAlarm) row.falseAlarms += 1
    if (event.contribution > row.top.contribution) row.top = event
    byType.set(event.type, row)
  }
  const rows = [...byType.values()].filter((row) => row.contribution > 0).sort((a, b) => b.contribution - a.contribution)
  const total = rows.reduce((sum, row) => sum + row.contribution, 0)
  const max = Math.max(1, ...rows.map((row) => row.contribution))

  if (rows.length === 0) return <p className="waterfall-empty">Skora katkı yapan olay yok.</p>

  return (
    <div className="bars">
      <ol className="bars-list">
        {rows.map((row) => {
          const share = total > 0 ? Math.round((row.contribution / total) * 100) : 0
          return (
            <li key={row.type}>
              <button
                type="button"
                className={['bars-row', selectedType === row.type && 'is-selected'].filter(Boolean).join(' ')}
                onClick={() => onSelect(row.top)}
                onMouseEnter={() => setHover(row.type)}
                onMouseLeave={() => setHover(null)}
                onFocus={() => setHover(row.type)}
                onBlur={() => setHover(null)}
                aria-label={`${eventLabel(row.type)}: skora ${formatNumber(row.contribution, 1)} puan, toplamın yüzde ${share}'i, ${row.count} olay`}
              >
                <span className="bars-label">{eventLabel(row.type)}</span>
                <span className="bars-track" aria-hidden="true">
                  <span className="bars-fill" style={{ width: `${(row.contribution / max) * 100}%` }} />
                </span>
                <span className="bars-value numeric">+{formatNumber(row.contribution, 1)}</span>
                {hover === row.type ? (
                  <span className="bars-tip" role="tooltip">
                    <strong>{eventLabel(row.type)}</strong>
                    <span>
                      {row.count} olay · toplam {formatDuration(row.seconds)}
                    </span>
                    <span>Skorun %{share}'i</span>
                    {row.falseAlarms ? <span>{row.falseAlarms} yanlış alarm hariç</span> : null}
                  </span>
                ) : null}
              </button>
            </li>
          )
        })}
      </ol>
      <p className="bars-total">
        <span>Toplam katkı</span>
        <strong className="numeric">{formatNumber(total, 1)}</strong>
      </p>
    </div>
  )
}
