import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AUDIO_CHUNK_MS, FRAME_INTERVAL_MS, startMediaMonitor, type MediaMonitorDeps, type MediaMonitorHandlers, type RecorderLike } from './mediaMonitor'

class FakeTrack extends EventTarget {
  readonly kind: 'video' | 'audio'
  constructor(kind: 'video' | 'audio') {
    super()
    this.kind = kind
  }
  end() {
    this.dispatchEvent(new Event('ended'))
  }
}

function fakeStream(tracks: FakeTrack[]): MediaStream {
  return { getTracks: () => tracks } as unknown as MediaStream
}

class FakeRecorder implements RecorderLike {
  state: 'inactive' | 'recording' = 'inactive'
  ondataavailable: ((event: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  starts = 0
  start() {
    this.state = 'recording'
    this.starts += 1
  }
  stop() {
    if (this.state !== 'recording') return
    this.state = 'inactive'
    // Gerçek MediaRecorder gibi: önce veri, sonra stop; her parça kendi başına tam bir dosya.
    this.ondataavailable?.({ data: new Blob([`parca-${this.starts}`], { type: 'audio/webm' }) })
    this.onstop?.()
  }
}

describe('kamera ve ses gönderimi', () => {
  let video: FakeTrack
  let audio: FakeTrack
  let recorder: FakeRecorder
  let onFrame: ReturnType<typeof vi.fn<MediaMonitorHandlers['onFrame']>>
  let onAudio: ReturnType<typeof vi.fn<MediaMonitorHandlers['onAudio']>>
  let onTrackEnded: ReturnType<typeof vi.fn<MediaMonitorHandlers['onTrackEnded']>>
  let captureFrame: ReturnType<typeof vi.fn<MediaMonitorDeps['captureFrame']>>

  function start(createRecorder: () => RecorderLike | null = () => recorder) {
    return startMediaMonitor(
      fakeStream([video, audio]),
      { onFrame, onAudio, onTrackEnded },
      { captureFrame, createRecorder },
    )
  }

  beforeEach(() => {
    vi.useFakeTimers()
    video = new FakeTrack('video')
    audio = new FakeTrack('audio')
    recorder = new FakeRecorder()
    onFrame = vi.fn<MediaMonitorHandlers['onFrame']>(async () => undefined)
    onAudio = vi.fn<MediaMonitorHandlers['onAudio']>(async () => undefined)
    onTrackEnded = vi.fn<MediaMonitorHandlers['onTrackEnded']>()
    captureFrame = vi.fn<MediaMonitorDeps['captureFrame']>(async () => new Blob(['jpeg'], { type: 'image/jpeg' }))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('saniyede bir kare gönderir', async () => {
    const stop = start()

    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 3)

    expect(onFrame).toHaveBeenCalledTimes(3)
    expect(onFrame.mock.calls[0][0]).toBeInstanceOf(Blob)
    stop()
  })

  it('önceki kare gönderilmeden yenisini göndermez; yavaş bağlantıda istek birikmez', async () => {
    let release: () => void = () => undefined
    onFrame.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => {
          release = resolve
        }),
    )
    const stop = start()

    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 3)
    expect(onFrame).toHaveBeenCalledTimes(1)

    release()
    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS)
    expect(onFrame).toHaveBeenCalledTimes(2)
    stop()
  })

  it('kare alınamazsa göndermez', async () => {
    captureFrame.mockResolvedValue(null)
    const stop = start()

    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 2)

    expect(onFrame).not.toHaveBeenCalled()
    stop()
  })

  it('sesi her biri tam dosya olan ~1 sn parçalar hâlinde gönderir', async () => {
    const stop = start()
    expect(recorder.state).toBe('recording')

    await vi.advanceTimersByTimeAsync(AUDIO_CHUNK_MS * 3)

    expect(onAudio).toHaveBeenCalledTimes(3)
    // Her parça için kayıt yeniden başlıyor; timeslice'ta yalnızca ilk parçada WebM başlığı olurdu.
    expect(recorder.starts).toBe(4)
    stop()
  })

  it('ses kaydı desteklenmiyorsa yalnızca kare gönderir', async () => {
    const stop = start(() => null)

    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 2)

    expect(onFrame).toHaveBeenCalledTimes(2)
    expect(onAudio).not.toHaveBeenCalled()
    stop()
  })

  it('kamera ya da mikrofon izni kalkınca bir kez bildirir ve gönderimi durdurur', async () => {
    start()

    video.end()
    audio.end()
    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 3)

    expect(onTrackEnded).toHaveBeenCalledTimes(1)
    expect(onFrame).not.toHaveBeenCalled()
    expect(recorder.state).toBe('inactive')
  })

  it('durdurulunca zamanlayıcıları ve kaydı bırakır', async () => {
    const stop = start()
    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS)
    stop()
    const frames = onFrame.mock.calls.length
    const chunks = onAudio.mock.calls.length

    await vi.advanceTimersByTimeAsync(FRAME_INTERVAL_MS * 5)
    video.end()

    expect(onFrame).toHaveBeenCalledTimes(frames)
    // Durdururken kayıttaki yarım parça gönderilmez.
    expect(onAudio).toHaveBeenCalledTimes(chunks)
    expect(recorder.state).toBe('inactive')
    expect(onTrackEnded).not.toHaveBeenCalled()
  })
})
