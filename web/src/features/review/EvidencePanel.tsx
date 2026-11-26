import { Flag, ImageOff } from 'lucide-react'
import { useState } from 'react'
import type { ReviewEvent } from '../../api/review'
import { Button } from '../../components/Button'
import { SOURCE_LABEL, eventLabel } from '../../lib/eventCatalog'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDuration, formatNumber, formatPercent, formatTimeSeconds } from '../../lib/format'

const messages = defineMessages({
  tr: {
    noEvents: 'Bu oturumda olay yok.',
    what: 'Ne oldu',
    source: 'Kaynak',
    when: 'Ne zaman',
    duration: 'Süre',
    instant: 'Anlık',
    effect: 'Skora etkisi',
    falseAlarmZero: '0 (yanlış alarm)',
    confidence: 'Model güveni',
    alt: (label: string) => `${label} anına ait kanıt karesi`,
    reveal: 'Kanıtı göster',
    deleted: 'Kanıt kareleri saklama süresi dolduğu için silindi.',
    expired: 'Kanıt bağlantısının süresi dolmuş olabilir; sayfayı yenileyin.',
    none: 'Bu olay için kanıt karesi yok.',
    falseAlarm: 'Yanlış alarm',
    mark: 'Yanlış alarm olarak işaretle',
  },
  en: {
    noEvents: 'There are no events in this session.',
    what: 'What happened',
    source: 'Source',
    when: 'When',
    duration: 'Duration',
    instant: 'Instant',
    effect: 'Effect on score',
    falseAlarmZero: '0 (false alarm)',
    confidence: 'Model confidence',
    alt: (label: string) => `Evidence frame at the moment of: ${label}`,
    reveal: 'Show evidence',
    deleted: 'Evidence frames were deleted because the retention period ended.',
    expired: 'The evidence link may have expired; reload the page.',
    none: 'There is no evidence frame for this event.',
    falseAlarm: 'False alarm',
    mark: 'Mark as false alarm',
  },
})

interface EvidencePanelProps {
  event: ReviewEvent | null
  monitoringDataDeletedAt: string | null
  /** Yanlış alarm işaretleyebilir mi (atanmış değerlendirici, karar verilmemiş). */
  canMark: boolean
  marking: boolean
  onMark: (event: ReviewEvent) => void
}

/**
 * Olay ayrıntısı: seçilen olayın ne olduğu, ne zaman ve ne kadar sürdüğü, skora etkisi ve kanıt karesi.
 * Yanlış alarm kararı burada verilir. Kare varsayılan bulanık; değerlendirici bilerek açar (5 dk imzalı bağlantı).
 */
export function EvidencePanel({ event, monitoringDataDeletedAt, canMark, marking, onMark }: EvidencePanelProps) {
  const m = useMessages(messages)
  const [revealedId, setRevealedId] = useState<string | null>(null)
  const [failedId, setFailedId] = useState<string | null>(null)

  if (!event) return <p className="evidence-empty">{m.noEvents}</p>

  const revealed = revealedId === event.id
  return (
    <div className="evidence">
      <dl className="evidence-facts">
        <div>
          <dt>{m.what}</dt>
          <dd>{event.reason ?? eventLabel(event.type)}</dd>
        </div>
        <div>
          <dt>{m.source}</dt>
          <dd>{SOURCE_LABEL[event.source]}</dd>
        </div>
        <div>
          <dt>{m.when}</dt>
          <dd className="numeric">{formatTimeSeconds(event.startedAt)}</dd>
        </div>
        <div>
          <dt>{m.duration}</dt>
          <dd className="numeric">{event.durationSec > 0 ? formatDuration(event.durationSec) : m.instant}</dd>
        </div>
        <div>
          <dt>{m.effect}</dt>
          <dd className="numeric">{event.isFalseAlarm ? m.falseAlarmZero : `+${formatNumber(event.contribution, 1)}`}</dd>
        </div>
        {event.confidence !== null ? (
          <div>
            <dt>{m.confidence}</dt>
            <dd className="numeric">{formatPercent(event.confidence * 100)}</dd>
          </div>
        ) : null}
      </dl>

      {event.evidenceUrl && failedId !== event.id ? (
        <figure className={['evidence-frame', revealed && 'is-revealed'].filter(Boolean).join(' ')}>
          <img src={event.evidenceUrl} alt={revealed ? m.alt(eventLabel(event.type)) : ''} onError={() => setFailedId(event.id)} />
          {revealed ? null : (
            <div className="evidence-veil">
              <Button variant="secondary" onClick={() => setRevealedId(event.id)}>
                {m.reveal}
              </Button>
            </div>
          )}
        </figure>
      ) : (
        <p className="evidence-none">
          <ImageOff size={18} aria-hidden="true" />
          {monitoringDataDeletedAt
            ? m.deleted
            : failedId === event.id
              ? m.expired
              : m.none}
        </p>
      )}

      {event.isFalseAlarm ? (
        <p className="evidence-marked">
          <Flag size={16} aria-hidden="true" />
          {m.falseAlarm}
          {event.falseAlarmMarkedBy ? ` · ${event.falseAlarmMarkedBy}` : ''}
        </p>
      ) : canMark && event.contribution > 0 ? (
        <Button variant="secondary" loading={marking} icon={<Flag size={16} aria-hidden="true" />} onClick={() => onMark(event)}>
          {m.mark}
        </Button>
      ) : null}
    </div>
  )
}
