import { useCallback, useEffect, useState, type RefObject } from 'react'

const CAMERA_ERROR = 'Kamera açılamadı. Tarayıcı izinlerini kontrol edip tekrar deneyin.'

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
      setError(CAMERA_ERROR)
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
  if (!context) throw new Error('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')

  const frames: Blob[] = []
  for (let index = 0; index < count; index += 1) {
    context.drawImage(video, 0, 0, width, height)
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob((value) => resolve(value), 'image/jpeg', 0.92))
    if (!blob) throw new Error('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')
    frames.push(blob)
    if (index < count - 1) await new Promise((resolve) => setTimeout(resolve, 150))
  }
  return frames
}
