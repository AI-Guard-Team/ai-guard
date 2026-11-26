import { UserRound } from 'lucide-react'
import type { ReactNode, RefObject } from 'react'
import './CameraView.css'

interface CameraViewProps {
  videoRef: RefObject<HTMLVideoElement | null>
  live: boolean
  /** Kamera kapalıyken ortada görünen açıklama ve düğme. */
  placeholder: ReactNode
  /** Yüz kılavuzu (oval) gösterilsin mi. */
  guide?: boolean
  label: string
}

/**
 * Kamera önizlemesi. Kutu kamera açılmadan önce de 4:3 oranında yer kaplar; görüntü gelince sayfa kaymaz.
 * Görüntü aynalanır (adaya ayna gibi görünür); gönderilen kare aynalanmaz.
 */
export function CameraView({ videoRef, live, placeholder, guide = true, label }: CameraViewProps) {
  return (
    <div className={['camera-view', live && 'is-live'].filter(Boolean).join(' ')}>
      <video ref={videoRef} className="camera-video" muted playsInline aria-label={label} />
      {live && guide ? <span className="camera-guide" aria-hidden="true" /> : null}
      {!live ? (
        <div className="camera-placeholder">
          <UserRound size={40} aria-hidden="true" />
          {placeholder}
        </div>
      ) : null}
    </div>
  )
}
