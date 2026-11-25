import type { BrowserEventType } from '../../api/sessions'

/** Aynı türden olay en fazla bu sıklıkla gönderilir (kart: saniyede bir). Sunucu da ayrıca sınırlıyor (K-16). */
export const EVENT_THROTTLE_MS = 1_000

/**
 * Aday sekmeye ya da pencereye hiç dönmese de ayrılma bu aralıkla, o ana kadarki süresiyle bildirilir.
 * Yoksa pencereyi arkada bırakıp hiç dönmeyen adayın ayrılması kaydedilmezdi.
 */
export const AWAY_FLUSH_MS = 60_000

export type BrowserEventSender = (type: BrowserEventType, durationSec: number) => void

function seconds(ms: number): number {
  return Math.round(ms) / 1000
}

/**
 * Sınav sırasında sekme/odak kaybı, tam ekrandan çıkış, kopyala-yapıştır ve sağ tık olaylarını dinler (S4, FR-14).
 * Olaylar engellenmez, yalnızca bildirilir. Dönen işlev dinlemeyi bırakır.
 */
export function watchBrowserEvents(send: BrowserEventSender): () => void {
  const lastSent = new Map<BrowserEventType, number>()
  let awaySince: number | null = null
  let flushTimer: number | undefined
  let wasFullscreen = document.fullscreenElement != null

  const emit = (type: BrowserEventType, durationSec = 0) => {
    const now = Date.now()
    const last = lastSent.get(type)
    if (last !== undefined && now - last < EVENT_THROTTLE_MS) return
    lastSent.set(type, now)
    send(type, durationSec)
  }

  const leave = () => {
    if (awaySince !== null) return
    awaySince = Date.now()
    flushTimer = window.setInterval(() => {
      if (awaySince === null) return
      const now = Date.now()
      emit('TAB_BLUR', seconds(now - awaySince))
      awaySince = now
    }, AWAY_FLUSH_MS)
  }

  const back = () => {
    if (awaySince === null) return
    const duration = seconds(Date.now() - awaySince)
    awaySince = null
    window.clearInterval(flushTimer)
    emit('TAB_BLUR', duration)
  }

  // visibilitychange sekme değişimini, blur/focus başka pencereye ya da uygulamaya geçişi yakalar; ikisi birlikte
  // gelirse tek ayrılma sayılır.
  const onVisibility = () => (document.visibilityState === 'hidden' ? leave() : back())
  const onFullscreen = () => {
    const now = document.fullscreenElement != null
    if (wasFullscreen && !now) emit('FULLSCREEN_EXIT')
    wasFullscreen = now
  }
  const onCopyPaste = () => emit('COPY_PASTE')
  const onContextMenu = () => emit('RIGHT_CLICK')

  document.addEventListener('visibilitychange', onVisibility)
  window.addEventListener('blur', leave)
  window.addEventListener('focus', back)
  document.addEventListener('fullscreenchange', onFullscreen)
  document.addEventListener('copy', onCopyPaste)
  document.addEventListener('cut', onCopyPaste)
  document.addEventListener('paste', onCopyPaste)
  document.addEventListener('contextmenu', onContextMenu)

  return () => {
    window.clearInterval(flushTimer)
    awaySince = null
    document.removeEventListener('visibilitychange', onVisibility)
    window.removeEventListener('blur', leave)
    window.removeEventListener('focus', back)
    document.removeEventListener('fullscreenchange', onFullscreen)
    document.removeEventListener('copy', onCopyPaste)
    document.removeEventListener('cut', onCopyPaste)
    document.removeEventListener('paste', onCopyPaste)
    document.removeEventListener('contextmenu', onContextMenu)
  }
}
