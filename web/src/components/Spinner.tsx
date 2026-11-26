import { LoaderCircle } from 'lucide-react'
import './Spinner.css'

/** Bekleme göstergesi. Ekran okuyucuya ayrıca söylenmez; çağıran yer aria-busy veya metinle bildirir. */
export function Spinner({ size = 20 }: { size?: number }) {
  return <LoaderCircle className="spinner" size={size} aria-hidden="true" />
}
