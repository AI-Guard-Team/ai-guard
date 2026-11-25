import { Flag, ImageOff } from 'lucide-react'
import { useState } from 'react'
import type { ReviewEvent } from '../../api/review'
import { Button } from '../../components/Button'
import { SOURCE_LABEL, eventLabel } from '../../lib/eventCatalog'
import { formatDuration, formatNumber, formatTimeSeconds } from '../../lib/format'

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
  const [revealedId, setRevealedId] = useState<string | null>(null)
  const [failedId, setFailedId] = useState<string | null>(null)

  if (!event) return <p className="evidence-empty">Bu oturumda olay yok.</p>

  const revealed = revealedId === event.id
  return (
    <div className="evidence">
      <dl className="evidence-facts">
        <div>
          <dt>Ne oldu</dt>
          <dd>{event.reason ?? eventLabel(event.type)}</dd>
        </div>
        <div>
          <dt>Kaynak</dt>
          <dd>{SOURCE_LABEL[event.source]}</dd>
        </div>
        <div>
          <dt>Ne zaman</dt>
          <dd className="numeric">{formatTimeSeconds(event.startedAt)}</dd>
        </div>
        <div>
          <dt>Süre</dt>
          <dd className="numeric">{event.durationSec > 0 ? formatDuration(event.durationSec) : 'Anlık'}</dd>
        </div>
        <div>
          <dt>Skora etkisi</dt>
          <dd className="numeric">{event.isFalseAlarm ? '0 (yanlış alarm)' : `+${formatNumber(event.contribution, 1)}`}</dd>
        </div>
        {event.confidence !== null ? (
          <div>
            <dt>Model güveni</dt>
            <dd className="numeric">%{formatNumber(event.confidence * 100)}</dd>
          </div>
        ) : null}
      </dl>

      {event.evidenceUrl && failedId !== event.id ? (
        <figure className={['evidence-frame', revealed && 'is-revealed'].filter(Boolean).join(' ')}>
          <img src={event.evidenceUrl} alt={revealed ? `${eventLabel(event.type)} anına ait kanıt karesi` : ''} onError={() => setFailedId(event.id)} />
          {revealed ? null : (
            <div className="evidence-veil">
              <Button variant="secondary" onClick={() => setRevealedId(event.id)}>
                Kanıtı göster
              </Button>
            </div>
          )}
        </figure>
      ) : (
        <p className="evidence-none">
          <ImageOff size={18} aria-hidden="true" />
          {monitoringDataDeletedAt
            ? 'Kanıt kareleri saklama süresi dolduğu için silindi.'
            : failedId === event.id
              ? 'Kanıt bağlantısının süresi dolmuş olabilir; sayfayı yenileyin.'
              : 'Bu olay için kanıt karesi yok.'}
        </p>
      )}

      {event.isFalseAlarm ? (
        <p className="evidence-marked">
          <Flag size={16} aria-hidden="true" />
          Yanlış alarm{event.falseAlarmMarkedBy ? ` · ${event.falseAlarmMarkedBy}` : ''}
        </p>
      ) : canMark && event.contribution > 0 ? (
        <Button variant="secondary" loading={marking} icon={<Flag size={16} aria-hidden="true" />} onClick={() => onMark(event)}>
          Yanlış alarm olarak işaretle
        </Button>
      ) : null}
    </div>
  )
}
