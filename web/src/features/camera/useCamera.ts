import { useCallback, useEffect, useState, type RefObject } from 'react'
import { defineMessages, pick } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    camera: 'Kamera açılamadı. Tarayıcı izinlerini kontrol edip tekrar deneyin.',
    denied: 'Kamera izni verilmedi. Adres çubuğunun solundaki kamera simgesine tıklayıp "İzin ver"i seçin, sonra yeniden deneyin.',
    notFound: 'Bu cihazda kamera bulunamadı. Bir kamera takıp yeniden deneyin ya da fotoğrafı dosyadan seçin.',
    busy: 'Kamera başka bir uygulama tarafından kullanılıyor (ör. Zoom, Teams). O uygulamayı kapatıp yeniden deneyin.',
    insecure: 'Tarayıcı kameraya yalnızca güvenli (https) bağlantıda izin verir. Sayfayı https adresinden açın.',
    frame: 'Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.',
  },
  en: {
    camera: 'The camera could not be opened. Check your browser permissions and try again.',
    denied: 'Camera permission was not granted. Click the camera icon on the left of the address bar, choose "Allow", then try again.',
    notFound: 'No camera was found on this device. Connect a camera and try again, or choose the photo from a file.',
    busy: 'The camera is in use by another app (e.g. Zoom, Teams). Close that app and try again.',
    insecure: 'The browser only allows the camera on a secure (https) connection. Open the page from its https address.',
    frame: 'A frame could not be captured. Reopen the camera and try again.',
  },
})

/** Tarayıcının kamera hatasını adayın yapabileceği işe çevirir (izin, cihaz yok, cihaz meşgul, güvensiz bağlantı). */
export function cameraErrorMessage(error: unknown): string {
  const m = pick(messages)
  if (typeof navigator !== 'undefined' && !navigator.mediaDevices?.getUserMedia) return m.insecure
  const name = error instanceof DOMException ? error.name : ''
  if (name === 'NotAllowedError' || name === 'SecurityError' || name === 'PermissionDeniedError') return m.denied
  if (name === 'NotFoundError' || name === 'OverconstrainedError' || name === 'DevicesNotFoundError') return m.notFound
  if (name === 'NotReadableError' || name === 'TrackStartError' || name === 'AbortError') return m.busy
  return m.camera
}

/** Kare alınamadığında gösterilen metin; kayıt fotoğrafı ekranı da kullanır. */
export const frameError = () => pick(messages).frame

export function useCamera(videoRef: RefObject<HTMLVideoElement | null>) {
  const [stream, setStream] = useState<MediaStream | null>(null)
  const [error, setError] = useState<string | null>(null)

  const stop = useCallback(() => {
    setStream((current) => {
      current?.getTracks().forEach((track) => track.stop())
      return null
    })
  }, [])

  const start = useCallback(async () => {
    setError(null)
    stop()
    try {
      setStream(await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' } }))
    } catch (caught) {
      setError(cameraErrorMessage(caught))
    }
  }, [stop])

  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    video.srcObject = stream
    if (stream) void video.play?.()
    return () => {
      video.srcObject = null
    }
  }, [stream, videoRef])

  useEffect(() => stop, [stop])

  return { live: stream !== null, error, start, stop }
}

export async function captureFrames(video: HTMLVideoElement, count = 3): Promise<Blob[]> {
  const canvas = document.createElement('canvas')
  const width = video.videoWidth || 640
  const height = video.videoHeight || 480
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')
  if (!context) throw new Error(frameError())

  const frames: Blob[] = []
  for (let index = 0; index < count; index += 1) {
    context.drawImage(video, 0, 0, width, height)
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob((value) => resolve(value), 'image/jpeg', 0.92))
    if (!blob) throw new Error(frameError())
    frames.push(blob)
    if (index < count - 1) await new Promise((resolve) => setTimeout(resolve, 150))
  }
  return frames
}
