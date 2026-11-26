import { useRef, type KeyboardEvent, type PointerEvent } from 'react'
import type { EventSource, ReviewEvent, ScorePoint } from '../../api/review'
import { SOURCE_LABEL, SOURCES, eventLabel } from '../../lib/eventCatalog'
import { formatTime, formatTimeSeconds } from '../../lib/format'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: { timeline: 'Zaman çizgisi', lane: (source: string) => `${source} olayları`, falseAlarm: ', yanlış alarm' },
  en: { timeline: 'Timeline', lane: (source: string) => `${source} events`, falseAlarm: ', false alarm' },
})

interface SignalStripProps {
  start: number
  end: number
  events: ReviewEvent[]
  scoreHistory: ScorePoint[]
  /** Oynatma çizgisinin zamanı (ms). */
  cursor: number
  selectedId: string | null
  onCursor: (time: number) => void
  onSelect: (event: ReviewEvent) => void
}

const MIN_WIDTH_PERCENT = 0.6
const CURVE_HEIGHT = 56

/**
 * Sinyal kaydı (Prompt F, ekranın imza öğesi): yatay zaman ekseni, her kaynak için bir şerit, olaylar kendi
 * şeritlerinde süreleri kadar blok. Üstte sunucunun skor geçmişi (ScoreHistory) basamaklı eğri olarak.
 * Oynatma çizgisi fareyle sürüklenir ya da ok tuşlarıyla kayar; bloklar düğme, klavyeyle de seçilir.
 */
export function SignalStrip({ start, end, events, scoreHistory, cursor, selectedId, onCursor, onSelect }: SignalStripProps) {
  const m = useMessages(messages)
  const track = useRef<HTMLDivElement>(null)
  const span = Math.max(1, end - start)
  const percent = (time: number) => Math.min(100, Math.max(0, ((time - start) / span) * 100))

  const timeAt = (clientX: number) => {
    const rect = track.current?.getBoundingClientRect()
    if (!rect || rect.width === 0) return start
    return start + Math.min(1, Math.max(0, (clientX - rect.left) / rect.width)) * span
  }

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if ((event.target as HTMLElement).closest('.signal-block')) return
    event.currentTarget.setPointerCapture(event.pointerId)
    onCursor(timeAt(event.clientX))
  }
  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) onCursor(timeAt(event.clientX))
  }
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.shiftKey ? 60_000 : 10_000
    if (event.key === 'ArrowRight') onCursor(Math.min(end, cursor + step))
    else if (event.key === 'ArrowLeft') onCursor(Math.max(start, cursor - step))
    else if (event.key === 'Home') onCursor(start)
    else if (event.key === 'End') onCursor(end)
    else return
    event.preventDefault()
  }

  // Eksen işaretleri: süreye göre 5, 10 ya da 15 dakikada bir.
  const minutes = span / 60_000
  const tickEvery = (minutes <= 30 ? 5 : minutes <= 90 ? 10 : 15) * 60_000
  const ticks: number[] = []
  for (let time = Math.ceil(start / tickEvery) * tickEvery; time <= end; time += tickEvery) ticks.push(time)

  const maxScore = Math.max(1, ...scoreHistory.map((point) => point.score))
  const curve = scoreHistory.length
    ? [
        `M 0 ${CURVE_HEIGHT}`,
        ...scoreHistory.flatMap((point, index) => {
          const x = percent(new Date(point.at).getTime())
          const y = CURVE_HEIGHT - (point.score / maxScore) * (CURVE_HEIGHT - 6)
          const previousY = index === 0 ? CURVE_HEIGHT : CURVE_HEIGHT - (scoreHistory[index - 1].score / maxScore) * (CURVE_HEIGHT - 6)
          return [`L ${x} ${previousY}`, `L ${x} ${y}`]
        }),
        `L 100 ${CURVE_HEIGHT - (scoreHistory[scoreHistory.length - 1].score / maxScore) * (CURVE_HEIGHT - 6)}`,
      ].join(' ')
    : ''

  const bySource = (source: EventSource) => events.filter((item) => item.source === source)

  return (
    <div className="signal-strip">
      <div className="signal-labels" aria-hidden="true">
        <span className="signal-label signal-label-curve">Skor</span>
        {SOURCES.map((source) => (
          <span key={source} className="signal-label">
            {SOURCE_LABEL[source]}
          </span>
        ))}
        <span className="signal-label signal-label-axis" />
      </div>

      <div
        ref={track}
        className="signal-track"
        role="slider"
        tabIndex={0}
        aria-label={m.timeline}
        aria-valuemin={start}
        aria-valuemax={end}
        aria-valuenow={Math.round(cursor)}
        aria-valuetext={formatTimeSeconds(new Date(cursor))}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onKeyDown={onKeyDown}
      >
        <svg className="signal-curve" viewBox={`0 0 100 ${CURVE_HEIGHT}`} preserveAspectRatio="none" aria-hidden="true">
          {curve ? <path d={curve} vectorEffect="non-scaling-stroke" /> : null}
        </svg>

        {SOURCES.map((source) => (
          <div key={source} className="signal-lane" role="group" aria-label={m.lane(SOURCE_LABEL[source])}>
            {bySource(source).map((item) => {
              const at = new Date(item.startedAt).getTime()
              const width = Math.max(MIN_WIDTH_PERCENT, ((item.durationSec * 1000) / span) * 100)
              const tone = item.isFalseAlarm ? 'is-false-alarm' : item.contribution > 0 ? 'is-scored' : 'is-info'
              return (
                <button
                  key={item.id}
                  type="button"
                  className={['signal-block', tone, item.id === selectedId && 'is-selected'].filter(Boolean).join(' ')}
                  style={{ left: `${percent(at)}%`, width: `${Math.min(width, 100 - percent(at))}%` }}
                  aria-label={`${eventLabel(item.type)}, ${formatTimeSeconds(item.startedAt)}${item.isFalseAlarm ? m.falseAlarm : ''}`}
                  title={`${eventLabel(item.type)} · ${formatTimeSeconds(item.startedAt)}`}
                  onClick={() => onSelect(item)}
                />
              )
            })}
          </div>
        ))}

        <div className="signal-axis" aria-hidden="true">
          {ticks.map((time) => (
            <span key={time} style={{ left: `${percent(time)}%` }}>
              {formatTime(new Date(time))}
            </span>
          ))}
        </div>

        <span className="signal-cursor" style={{ left: `${percent(cursor)}%` }} aria-hidden="true">
          <span className="signal-cursor-time">{formatTimeSeconds(new Date(cursor))}</span>
        </span>
      </div>
    </div>
  )
}
