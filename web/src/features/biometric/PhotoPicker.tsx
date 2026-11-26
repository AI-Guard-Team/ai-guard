import { Camera, ImageUp } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { Button } from '../../components/Button'
import { CameraView } from '../camera/CameraView'
import { useCamera } from '../camera/useCamera'
import { ACCEPTED_PHOTO_TYPES } from './prepareReferencePhoto'
import './PhotoPicker.css'

interface PhotoPickerProps {
  /** Rıza verilmeden dosya seçimi ve kamera kapalı. */
  disabled: boolean
  previewUrl: string | null
  onPick: (file: File) => void
}

/** Kayıt fotoğrafı seçimi: sürükle-bırak, dosya ya da kamera. Hazırlama ve gönderme çağıran yerde. */
export function PhotoPicker({ disabled, previewUrl, onPick }: PhotoPickerProps) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const camera = useCamera(videoRef)
  const [dragging, setDragging] = useState(false)
  const [captureError, setCaptureError] = useState<string | null>(null)

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    if (disabled) return
    const file = event.dataTransfer.files?.[0]
    if (file) onPick(file)
  }

  const takePhoto = () => {
    const video = videoRef.current
    if (!video) return
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth || 640
    canvas.height = video.videoHeight || 480
    const context = canvas.getContext('2d')
    if (!context) {
      setCaptureError('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')
      return
    }
    context.drawImage(video, 0, 0, canvas.width, canvas.height)
    canvas.toBlob(
      (blob) => {
        if (!blob) {
          setCaptureError('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')
          return
        }
        setCaptureError(null)
        onPick(new File([blob], 'kayit-fotografi.jpg', { type: 'image/jpeg' }))
        camera.stop()
      },
      'image/jpeg',
      0.92,
    )
  }

  if (camera.live) {
    return (
      <div className="photo-picker">
        <CameraView videoRef={videoRef} live label="Kayıt fotoğrafı için kamera" placeholder={null} />
        <div className="photo-picker-actions">
          <Button onClick={takePhoto} icon={<Camera size={18} aria-hidden="true" />}>
            Bu kareyi kullan
          </Button>
          <Button variant="secondary" onClick={camera.stop}>
            Vazgeç
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="photo-picker">
      <div
        className={['photo-drop', dragging && 'is-dragging', disabled && 'is-disabled', previewUrl && 'has-preview'].filter(Boolean).join(' ')}
        onDragOver={(event) => {
          event.preventDefault()
          if (!disabled) setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
      >
        {previewUrl ? (
          <img className="photo-preview" src={previewUrl} alt="Seçilen kayıt fotoğrafı" />
        ) : (
          <>
            <ImageUp size={32} aria-hidden="true" />
            <p className="photo-drop-title">Fotoğrafı buraya bırakın ya da seçin</p>
            <p className="photo-drop-hint">JPEG, PNG veya WebP. Büyük fotoğraflar göndermeden önce küçültülür.</p>
          </>
        )}
      </div>
      <input
        ref={fileRef}
        id="reference-photo"
        className="visually-hidden"
        type="file"
        accept={ACCEPTED_PHOTO_TYPES.join(',')}
        disabled={disabled}
        aria-label="Fotoğraf dosyası seç"
        onChange={(event) => {
          const input = event.target
          const file = input.files?.[0]
          input.value = ''
          if (file) onPick(file)
        }}
      />
      <div className="photo-picker-actions">
        <Button variant="secondary" disabled={disabled} onClick={() => fileRef.current?.click()} icon={<ImageUp size={18} aria-hidden="true" />}>
          {previewUrl ? 'Başka dosya seç' : 'Dosya seç'}
        </Button>
        <Button variant="secondary" disabled={disabled} onClick={() => void camera.start()} icon={<Camera size={18} aria-hidden="true" />}>
          Kameradan çek
        </Button>
      </div>
      {camera.error || captureError ? (
        <p className="field-error" role="alert">
          {camera.error ?? captureError}
        </p>
      ) : null}
    </div>
  )
}
