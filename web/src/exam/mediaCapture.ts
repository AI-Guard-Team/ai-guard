import type { MediaMonitorDeps, RecorderLike } from './mediaMonitor'

/** Kare genişliği bunu aşarsa küçültülür; yüz ve telefon tespiti için yeterli, kare 1 MB sınırının çok altında. */
const MAX_FRAME_WIDTH = 640
const JPEG_QUALITY = 0.8

/** Önce Opus; görüntü servisi WebM'i 16 kHz mono'ya kendisi dönüştürüyor (S-22). */
const AUDIO_TYPES = ['audio/webm;codecs=opus', 'audio/webm']

/** Sınav sırasında kamera ve mikrofon. Görüntü ekranda gösterilmiyor; kare görünmeyen bir video öğesinden alınıyor. */
export function openExamStream(): Promise<MediaStream> {
  return navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: true })
}

export function stopStream(stream: MediaStream | null | undefined): void {
  stream?.getTracks().forEach((track) => track.stop())
}

export function isStreamLive(stream: MediaStream | null | undefined): stream is MediaStream {
  return !!stream && stream.getTracks().length > 0 && stream.getTracks().every((track) => track.readyState === 'live')
}

/**
 * Akıştan kare ve ses alan tarayıcı uygulaması. `dispose` video öğesini bırakır; akışı durdurmaz.
 * Gönderim zamanlaması `mediaMonitor`'da; bu dosya yalnızca tarayıcı API'lerini (video, canvas, MediaRecorder) kullanıyor,
 * böylece zamanlama testleri gerçek kamera olmadan yazılabiliyor.
 */
export function createBrowserCapture(stream: MediaStream): MediaMonitorDeps & { dispose(): void } {
  const video = document.createElement('video')
  video.muted = true
  video.playsInline = true
  video.srcObject = stream
  void video.play?.()?.catch(() => undefined)
  const canvas = document.createElement('canvas')

  return {
    // Videonun o anki görüntüsünü küçültüp JPEG'e çevirir. Video henüz görüntü vermiyorsa (açılış) kare atlanır.
    captureFrame() {
      if (video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA || !video.videoWidth) return Promise.resolve(null)
      const scale = Math.min(1, MAX_FRAME_WIDTH / video.videoWidth)
      canvas.width = Math.round(video.videoWidth * scale)
      canvas.height = Math.round(video.videoHeight * scale)
      const context = canvas.getContext('2d')
      if (!context) return Promise.resolve(null)
      context.drawImage(video, 0, 0, canvas.width, canvas.height)
      return new Promise((resolve) => canvas.toBlob((blob) => resolve(blob), 'image/jpeg', JPEG_QUALITY))
    },
    // Yalnızca mikrofon izinden kayıt; görüntü ses parçasına girmesin. 32 kbit/sn konuşma tespiti için yeterli.
    createRecorder() {
      if (typeof MediaRecorder === 'undefined') return null
      const audio = stream.getAudioTracks()
      const mimeType = AUDIO_TYPES.find((type) => MediaRecorder.isTypeSupported(type))
      if (audio.length === 0 || !mimeType) return null
      return new MediaRecorder(new MediaStream(audio), { mimeType, audioBitsPerSecond: 32_000 }) as unknown as RecorderLike
    },
    dispose() {
      video.srcObject = null
    },
  }
}
