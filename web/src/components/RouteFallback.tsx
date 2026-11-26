import { Spinner } from './Spinner'
import { defineMessages, useMessages } from '../i18n/messages'
import './RouteFallback.css'

const messages = defineMessages({ tr: { loading: 'Sayfa yükleniyor' }, en: { loading: 'Loading page' } })

/**
 * Sayfa parçası inerken gösterilir. Kısa bekleyişte yanıp sönmesin diye gösterge 300 ms gecikmeyle belirir;
 * yer tutucu tam yükseklikte olduğundan sayfa gelince içerik kaymaz.
 */
export function RouteFallback() {
  const m = useMessages(messages)
  return (
    <div className="route-fallback" role="status" aria-live="polite">
      <Spinner size={28} />
      <span className="visually-hidden">{m.loading}</span>
    </div>
  )
}
