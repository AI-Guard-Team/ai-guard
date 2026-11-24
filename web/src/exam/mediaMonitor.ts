/** Kamera karesi aralığı (kart: saniyede 1 kare). */
export const FRAME_INTERVAL_MS = 1_000

/** Ses parçası uzunluğu (kart: ~1 sn, S-22). */
export const AUDIO_CHUNK_MS = 1_000

/** MediaRecorder'ın burada kullanılan kısmı; testlerde sahtesi veriliyor. */
export interface RecorderLike {
  readonly state: string
  ondataavailable: ((event: { data: Blob }) => void) | null
  onstop: (() => void) | null
  start(): void
  stop(): void
}

export interface MediaMonitorHandlers {
  onFrame(frame: Blob): Promise<void>
  onAudio(chunk: Blob): Promise<void>
  /** Kamera ya da mikrofon izni kalktı (track "ended"); gönderim durmuş olur. Bir kez çağrılır. */
  onTrackEnded(): void
}

export interface MediaMonitorDeps {
  /** Akıştaki görüntüden JPEG kare; alınamazsa null. */
  captureFrame(): Promise<Blob | null>
  /** Ses kaydedici; tarayıcı WebM/Opus kaydedemiyorsa ya da mikrofon yoksa null. */
  createRecorder(): RecorderLike | null
}

/**
 * Sınav sırasında kareleri ve ses parçalarını gönderir (S4, FR-14). Kare ve ses yalnızca bellekte, gönderilene
 * kadar tutulur; kuyruğa ya da tarayıcı depolamasına yazılmaz. Dönen işlev gönderimi durdurur; akışın kendisini
 * (izin ve kamera ışığı) çağıran yönetir.
 */
export function startMediaMonitor(stream: MediaStream, handlers: MediaMonitorHandlers, deps: MediaMonitorDeps): () => void {
  let running = true
  let frameBusy = false
  let chunkTimer: number | undefined
  const tracks = stream.getTracks()

  const frameTimer = window.setInterval(() => {
    // Önceki kare henüz gitmediyse bu saniye atlanır: yavaş bağlantıda istekler üst üste binmesin.
    if (!running || frameBusy) return
    frameBusy = true
    void deps
      .captureFrame()
      .then((frame) => (frame && running ? handlers.onFrame(frame) : undefined))
      .catch(() => undefined)
      .finally(() => {
        frameBusy = false
      })
  }, FRAME_INTERVAL_MS)

  // Her parça için kayıt yeniden başlatılıyor. timeslice ile tek kayıtta yalnızca ilk parçada WebM başlığı olur ve
  // sonraki parçalar tek başına çözülemez; görüntü servisi her parçayı ayrı çözüyor (docs/contracts.md 6. bölüm).
  const recorder = deps.createRecorder()
  if (recorder) {
    recorder.ondataavailable = (event) => {
      if (running && event.data.size > 0) void handlers.onAudio(event.data).catch(() => undefined)
    }
    recorder.onstop = () => {
      if (!running) return
      recorder.start()
      chunkTimer = window.setTimeout(() => recorder.stop(), AUDIO_CHUNK_MS)
    }
    recorder.start()
    chunkTimer = window.setTimeout(() => recorder.stop(), AUDIO_CHUNK_MS)
  }

  const stop = () => {
    if (!running) return
    running = false
    window.clearInterval(frameTimer)
    window.clearTimeout(chunkTimer)
    tracks.forEach((track) => track.removeEventListener('ended', onEnded))
    if (recorder?.state === 'recording') recorder.stop()
  }

  function onEnded() {
    if (!running) return
    stop()
    handlers.onTrackEnded()
  }

  tracks.forEach((track) => track.addEventListener('ended', onEnded))
  return stop
}
