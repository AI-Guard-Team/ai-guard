import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { reportPermission, sendAudio, sendBrowserEvent, sendFrame } from '../../api/sessions'
import { watchBrowserEvents } from './browserEvents'
import { createBrowserCapture, isStreamLive, openExamStream, stopStream } from './mediaCapture'
import { startMediaMonitor } from './mediaMonitor'
import { defineMessages, pick } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    denied: 'Kamera ve mikrofon izni verilmedi. Tarayıcının adres çubuğundaki kamera simgesinden izin verip yeniden deneyin.',
    unsupported: 'Bu tarayıcı kamera ve mikrofona erişime izin vermiyor. Güncel Chrome, Edge, Firefox ya da Safari kullanın.',
    resumeFailed: 'Sınava dönülemedi. Yeniden deneyin.',
  },
  en: {
    denied: 'Camera and microphone permission was not granted. Allow it from the camera icon in the address bar and try again.',
    unsupported: 'This browser does not allow access to the camera and microphone. Use an up-to-date Chrome, Edge, Firefox or Safari.',
    resumeFailed: 'Could not return to the exam. Try again.',
  },
})

interface Options {
  sessionId: string | undefined
  state: string | undefined
  /** Sunucudaki durum değişmiş olabilir (izin bildirildi, kare reddedildi); oturum yeniden okunmalı. */
  onStateChange: () => void
}

function supported(): boolean {
  return typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getUserMedia === 'function'
}

/**
 * Sınav sırasında sunucuya giden sinyaller (S4, FR-14, FR-17):
 * - InProgress: kamera ve mikrofon açık, saniyede bir kare ve ~1 sn ses parçası gidiyor.
 * - İzin kalkarsa sunucuya bildiriliyor; oturum PermissionPending olur, sorular gizlenir, süre işler.
 * - `requestPermission` izni yeniden alıp sınava döndürür; izinsiz süreyi sunucu ölçer (K-30).
 * - InProgress ve PermissionPending'de tarayıcı olayları gönderiliyor.
 * - `ready`: kamera ve mikrofon gerçekten açık. Sınav ekranı soruları yalnızca bu durumda gösteriyor; izin istemi
 *   yanıtlanmadan ya da izin verilmeden sorular görünmüyor.
 */
export function useExamMonitoring({ sessionId, state, onStateChange }: Options) {
  const streamRef = useRef<MediaStream | null>(null)
  const refresh = useRef(onStateChange)
  const [live, setLive] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [requesting, setRequesting] = useState(false)

  useEffect(() => {
    refresh.current = onStateChange
  }, [onStateChange])

  // İzin kalktı: kamerayı kapat, sunucuya bildir (oturum PermissionPending olur), sonra güncel durumu al.
  const revoke = useCallback(async () => {
    if (!sessionId) return
    stopStream(streamRef.current)
    streamRef.current = null
    setLive(false)
    try {
      await reportPermission(sessionId, false)
    } catch {
      // Bildirim gitmese de sunucu kare almadığı için izlemedeki boşluğu görüyor; durum yoklamada güncellenir.
    }
    refresh.current()
  }, [sessionId])

  // Tarayıcı olayları izin beklerken de gönderiliyor: aday o sırada başka sekmeye geçebilir.
  const watching = state === 'InProgress' || state === 'PermissionPending'

  useEffect(() => {
    if (!sessionId || !watching) return
    return watchBrowserEvents((type, durationSec) => {
      void sendBrowserEvent(sessionId, type, durationSec).catch(() => undefined)
    })
  }, [sessionId, watching])

  // Sınav sürerken: akışı aç (ya da izin ekranında alınanı kullan), kare/ses gönderimini başlat.
  // Durum değişince (izin, kesinti, teslim) temizleme işlevi gönderimi durduruyor.
  useEffect(() => {
    if (!sessionId || !supported() || state !== 'InProgress') return

    let cancelled = false
    let stopMonitor: (() => void) | undefined
    let stopPermissionWatch: (() => void) | undefined
    let dispose: (() => void) | undefined

    void (async () => {
      let stream = isStreamLive(streamRef.current) ? streamRef.current : null
      if (!stream) {
        try {
          stream = await openExamStream()
        } catch {
          // İzin reddedildi ya da kamera/mikrofon kullanılamıyor: izlenemeyen sınav sürmemeli (FR-17).
          if (!cancelled) {
            setError(pick(messages).denied)
            await revoke()
          }
          return
        }
        if (cancelled) {
          stopStream(stream)
          return
        }
        streamRef.current = stream
      }

      const capture = createBrowserCapture(stream)
      dispose = capture.dispose
      setError(null)
      setLive(true)
      const onRejected = (caught: unknown) => {
        // 409: oturum artık sürmüyor (izin, kesinti, teslim); güncel durumu al. Diğer hatalar yoklamaya kalır.
        if (caught instanceof ApiError && caught.status === 409) refresh.current()
      }

      // İzin iki yoldan kalkabilir: akış kapanır ("ended") ya da site izni "engelle" olur. Chrome adres çubuğundan
      // engellemede akışı her zaman kapatmıyor (S4 tarayıcı denemesi); bu yüzden izin durumu da dinleniyor.
      // Hangisi önce gelirse bir kez bildiriliyor.
      let ended = false
      const permissionLost = () => {
        if (ended || cancelled) return
        ended = true
        stopMonitor?.()
        setError(pick(messages).denied)
        void revoke()
      }

      stopMonitor = startMediaMonitor(
        stream,
        {
          onFrame: (frame) => sendFrame(sessionId, frame).catch(onRejected),
          onAudio: (chunk) => sendAudio(sessionId, chunk).catch(onRejected),
          onTrackEnded: permissionLost,
        },
        capture,
      )
      stopPermissionWatch = watchPermissions(permissionLost)
    })()

    return () => {
      cancelled = true
      stopMonitor?.()
      stopPermissionWatch?.()
      dispose?.()
    }
  }, [revoke, sessionId, state])

  // Sınav dışında kamera ve mikrofon kapalı (kamera ışığı söner). İzin beklerken alınan yeni akış korunur.
  // Bağlantı kesikken de akış açık kalıyor: aday çevrimdışı cevap vermeye devam edebiliyor (S3) ve dönüşte
  // yeniden izin istenmiyor; kare gönderimi bu sırada durmuş oluyor.
  useEffect(() => {
    if (state === 'InProgress' || state === 'PermissionPending' || state === 'Disconnected') return
    stopStream(streamRef.current)
    streamRef.current = null
    // Akış kapandı; "hazır" değeri aşağıda duruma göre türetiliyor, burada ayrıca güncellenmiyor.
  }, [state])

  useEffect(
    () => () => {
      stopStream(streamRef.current)
      streamRef.current = null
    },
    [],
  )

  // "Kamera ve mikrofona izin ver" düğmesi. İzin istemi yalnızca kullanıcı tıklamasıyla güvenilir açıldığı için
  // otomatik denenmiyor. Alınan akış saklanıyor; InProgress'e dönünce aynı akışla gönderim başlıyor.
  const requestPermission = useCallback(async () => {
    if (!sessionId) return
    if (!supported()) {
      setError(pick(messages).unsupported)
      return
    }
    setRequesting(true)
    setError(null)
    try {
      const stream = await openExamStream()
      streamRef.current = stream
      try {
        await reportPermission(sessionId, true)
      } catch (caught) {
        stopStream(stream)
        streamRef.current = null
        setError(caught instanceof ApiError ? caught.message : pick(messages).resumeFailed)
      }
      refresh.current()
    } catch {
      setError(pick(messages).denied)
    } finally {
      setRequesting(false)
    }
  }, [sessionId])

  return {
    // Gönderim yalnızca InProgress'te; başka durumda akış kapatılmış olur.
    live: live && state === 'InProgress',
    // Sorular yalnızca kamera ve mikrofon açıkken (S4). Bağlantı kesikken akış açık kaldığı için sorular da görünüyor.
    ready: live && (state === 'InProgress' || state === 'Disconnected'),
    error: supported() ? error : pick(messages).unsupported,
    requesting,
    requestPermission,
  }
}

/**
 * Kamera ve mikrofon izninin "engelle"ye çekilmesini dinler. Permissions API'yi desteklemeyen ya da "microphone" adını
 * tanımayan tarayıcıda (ör. Firefox) o izin için sessizce vazgeçiliyor; akışın kapanması yine yakalanıyor.
 */
function watchPermissions(onDenied: () => void): () => void {
  const permissions = typeof navigator !== 'undefined' ? navigator.permissions : undefined
  if (!permissions?.query) return () => undefined
  let active = true
  const cleanups: Array<() => void> = []
  for (const name of ['camera', 'microphone']) {
    permissions
      .query({ name: name as PermissionName })
      .then((status) => {
        if (!active) return
        const onChange = () => {
          if (status.state === 'denied') onDenied()
        }
        status.addEventListener('change', onChange)
        cleanups.push(() => status.removeEventListener('change', onChange))
        onChange()
      })
      .catch(() => undefined)
  }
  return () => {
    active = false
    cleanups.forEach((cleanup) => cleanup())
  }
}
