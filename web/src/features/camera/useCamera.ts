import { useCallback, useEffect, useState, type RefObject } from 'react'
import { defineMessages, pick } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    camera: 'Kamera açılamadı. Tarayıcı izinlerini kontrol edip tekrar deneyin.',
    frame: 'Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.',
  },
  en: {
    camera: 'The camera could not be opened. Check your browser permissions and try again.',
    frame: 'A frame could not be captured. Reopen the camera and try again.',
  },
})

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
    } catch {
      setError(pick(messages).camera)
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
