import { ImageOff } from 'lucide-react'
import { useState } from 'react'
import type { ReviewEvent } from '../../api/review'
import { Button } from '../../components/Button'
import { SOURCE_LABEL, eventLabel } from '../../lib/eventCatalog'
import { formatDuration, formatNumber, formatTimeSeconds } from '../../lib/format'

interface EvidencePanelProps {
  event: ReviewEvent | null
  monitoringDataDeletedAt: string | null
}

/**
 * Seçilen olayın ayrıntısı ve kanıt karesi. Kare varsayılan olarak bulanık; değerlendirici bilerek açar.
 * Bağlantı 5 dakikalık imzalı adres (K-31); süresi dolarsa sayfa yenilenince yenisi gelir.
 */
export function EvidencePanel({ event, monitoringDataDeletedAt }: EvidencePanelProps) {
  const [revealedId, setRevealedId] = useState<string | null>(null)
  const [failedId, setFailedId] = useState<string | null>(null)

  if (!event) {
    return <p className="evidence-empty">Sinyal kaydında ya da şelalede bir olay seçin; ayrıntısı ve varsa kanıt karesi burada açılır.</p>
  }

  const revealed = revealedId === event.id
  return (
    <div className="evidence">
      <p className="kicker">
        {SOURCE_LABEL[event.source]} · {formatTimeSeconds(event.startedAt)}
        {event.durationSec > 0 ? ` · ${formatDuration(event.durationSec)}` : ''}
      </p>
      <h3>{eventLabel(event.type)}</h3>
      {event.reason ? <p className="evidence-reason">{event.reason}</p> : null}
      <dl className="evidence-meta">
        <div>
          <dt>Katkı</dt>
          <dd className="numeric">{event.isFalseAlarm ? '0 (yanlış alarm)' : `+${formatNumber(event.contribution, 1)}`}</dd>
        </div>
        {event.confidence !== null ? (
          <div>
            <dt>Güven</dt>
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
    </div>
  )
}
