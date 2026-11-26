import { Camera, ImageUp } from 'lucide-react'
import { useRef, useState, type DragEvent } from 'react'
import { Button } from '../../components/Button'
import { CameraView } from '../camera/CameraView'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { frameError, useCamera } from '../camera/useCamera'
import { ACCEPTED_PHOTO_TYPES } from './prepareReferencePhoto'
import './PhotoPicker.css'

const messages = defineMessages({
  tr: {
    cameraLabel: 'Kayıt fotoğrafı için kamera',
    useFrame: 'Bu kareyi kullan',
    preview: 'Seçilen kayıt fotoğrafı',
    dropTitle: 'Fotoğrafı buraya bırakın ya da seçin',
    dropHint: 'JPEG, PNG veya WebP. Büyük fotoğraflar göndermeden önce küçültülür.',
    fileLabel: 'Fotoğraf dosyası seç',
    another: 'Başka dosya seç',
    choose: 'Dosya seç',
    camera: 'Kameradan çek',
    fileName: 'kayit-fotografi.jpg',
  },
  en: {
    cameraLabel: 'Camera for the reference photo',
    useFrame: 'Use this frame',
    preview: 'Selected reference photo',
    dropTitle: 'Drop the photo here or choose a file',
    dropHint: 'JPEG, PNG or WebP. Large photos are resized before upload.',
    fileLabel: 'Choose a photo file',
    another: 'Choose another file',
    choose: 'Choose file',
    camera: 'Take with camera',
    fileName: 'reference-photo.jpg',
  },
})

interface PhotoPickerProps {
  /** Rıza verilmeden dosya seçimi ve kamera kapalı. */
  disabled: boolean
  previewUrl: string | null
  onPick: (file: File) => void
}

/** Kayıt fotoğrafı seçimi: sürükle-bırak, dosya ya da kamera. Hazırlama ve gönderme çağıran yerde. */
export function PhotoPicker({ disabled, previewUrl, onPick }: PhotoPickerProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
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
      setCaptureError(frameError())
      return
    }
    context.drawImage(video, 0, 0, canvas.width, canvas.height)
    canvas.toBlob(
      (blob) => {
        if (!blob) {
          setCaptureError(frameError())
          return
        }
        setCaptureError(null)
        onPick(new File([blob], m.fileName, { type: 'image/jpeg' }))
        camera.stop()
      },
      'image/jpeg',
      0.92,
    )
  }

  if (camera.live) {
    return (
      <div className="photo-picker">
        <CameraView videoRef={videoRef} live label={m.cameraLabel} placeholder={null} />
        <div className="photo-picker-actions">
          <Button onClick={takePhoto} icon={<Camera size={18} aria-hidden="true" />}>
            {m.useFrame}
          </Button>
          <Button variant="secondary" onClick={camera.stop}>
            {c.cancel}
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
          <img className="photo-preview" src={previewUrl} alt={m.preview} />
        ) : (
          <>
            <ImageUp size={32} aria-hidden="true" />
            <p className="photo-drop-title">{m.dropTitle}</p>
            <p className="photo-drop-hint">{m.dropHint}</p>
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
        aria-label={m.fileLabel}
        onChange={(event) => {
          const input = event.target
          const file = input.files?.[0]
          input.value = ''
          if (file) onPick(file)
        }}
      />
      <div className="photo-picker-actions">
        <Button variant="secondary" disabled={disabled} onClick={() => fileRef.current?.click()} icon={<ImageUp size={18} aria-hidden="true" />}>
          {previewUrl ? m.another : m.choose}
        </Button>
        <Button variant="secondary" disabled={disabled} onClick={() => void camera.start()} icon={<Camera size={18} aria-hidden="true" />}>
          {m.camera}
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
