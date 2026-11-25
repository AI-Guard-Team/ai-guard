import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AWAY_FLUSH_MS, watchBrowserEvents, type BrowserEventSender } from './browserEvents'

function setVisibility(state: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state })
  document.dispatchEvent(new Event('visibilitychange'))
}

function setFullscreen(element: Element | null) {
  Object.defineProperty(document, 'fullscreenElement', { configurable: true, get: () => element })
  document.dispatchEvent(new Event('fullscreenchange'))
}

describe('tarayıcı olayları', () => {
  let send: ReturnType<typeof vi.fn<BrowserEventSender>>
  let stop: () => void

  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-24T10:00:00Z'))
    send = vi.fn<BrowserEventSender>()
    stop = watchBrowserEvents(send)
  })

  afterEach(() => {
    stop()
    setVisibility('visible')
    vi.useRealTimers()
  })

  it('sekmeden ayrılma süresini dönüşte TAB_BLUR olarak gönderir', () => {
    setVisibility('hidden')
    window.dispatchEvent(new Event('blur')) // ikisi birlikte gelir; tek ayrılma sayılır
    vi.advanceTimersByTime(4_500)
    setVisibility('visible')
    window.dispatchEvent(new Event('focus'))

    expect(send).toHaveBeenCalledTimes(1)
    expect(send).toHaveBeenCalledWith('TAB_BLUR', 4.5)
  })

  it('pencere odağı başka uygulamaya geçince de TAB_BLUR gönderir', () => {
    window.dispatchEvent(new Event('blur'))
    vi.advanceTimersByTime(2_000)
    window.dispatchEvent(new Event('focus'))

    expect(send).toHaveBeenCalledWith('TAB_BLUR', 2)
  })

  it('aday hiç dönmese de ayrılma belirli aralıklarla bildirilir', () => {
    window.dispatchEvent(new Event('blur'))
    vi.advanceTimersByTime(AWAY_FLUSH_MS * 2)

    expect(send).toHaveBeenCalledTimes(2)
    expect(send).toHaveBeenNthCalledWith(1, 'TAB_BLUR', AWAY_FLUSH_MS / 1000)
    expect(send).toHaveBeenNthCalledWith(2, 'TAB_BLUR', AWAY_FLUSH_MS / 1000)

    // Dönüşte yalnızca bildirilmemiş kalan süre gönderilir; toplam ayrılma süresi korunur.
    vi.advanceTimersByTime(3_000)
    window.dispatchEvent(new Event('focus'))
    expect(send).toHaveBeenLastCalledWith('TAB_BLUR', 3)
  })

  it('tam ekrandan çıkışı hemen FULLSCREEN_EXIT olarak gönderir, girişi göndermez', () => {
    setFullscreen(document.body)
    expect(send).not.toHaveBeenCalled()

    setFullscreen(null)
    expect(send).toHaveBeenCalledWith('FULLSCREEN_EXIT', 0)
  })

  it('kopyala, kes ve yapıştırı COPY_PASTE, sağ tıkı RIGHT_CLICK olarak gönderir', () => {
    document.dispatchEvent(new Event('copy'))
    vi.advanceTimersByTime(1_000)
    document.dispatchEvent(new Event('paste'))
    vi.advanceTimersByTime(1_000)
    document.dispatchEvent(new Event('cut'))
    document.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true }))

    expect(send.mock.calls).toEqual([
      ['COPY_PASTE', 0],
      ['COPY_PASTE', 0],
      ['COPY_PASTE', 0],
      ['RIGHT_CLICK', 0],
    ])
  })

  it('aynı olay saniyede en fazla bir kez gönderilir', () => {
    for (let i = 0; i < 5; i += 1) document.dispatchEvent(new MouseEvent('contextmenu'))
    vi.advanceTimersByTime(999)
    document.dispatchEvent(new MouseEvent('contextmenu'))
    vi.advanceTimersByTime(1)
    document.dispatchEvent(new MouseEvent('contextmenu'))

    expect(send).toHaveBeenCalledTimes(2)
  })

  it('durdurulunca dinlemeyi bırakır ve süren ayrılmayı göndermez', () => {
    window.dispatchEvent(new Event('blur'))
    stop()
    vi.advanceTimersByTime(AWAY_FLUSH_MS * 2)
    window.dispatchEvent(new Event('focus'))
    document.dispatchEvent(new Event('copy'))

    expect(send).not.toHaveBeenCalled()
  })
})
