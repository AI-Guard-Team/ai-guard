import { Spinner } from './Spinner'
import './RouteFallback.css'

/**
 * Sayfa parçası inerken gösterilir. Kısa bekleyişte yanıp sönmesin diye gösterge 300 ms gecikmeyle belirir;
 * yer tutucu tam yükseklikte olduğundan sayfa gelince içerik kaymaz.
 */
export function RouteFallback() {
  return (
    <div className="route-fallback" role="status" aria-live="polite">
      <Spinner size={28} />
      <span className="visually-hidden">Sayfa yükleniyor</span>
    </div>
  )
}
