import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { reportPermission, sendAudio, sendBrowserEvent, sendFrame } from '../../api/sessions'
import { watchBrowserEvents } from './browserEvents'
import { createBrowserCapture, isStreamLive, openExamStream, stopStream } from './mediaCapture'
import { startMediaMonitor } from './mediaMonitor'

const DENIED = 'Kamera ve mikrofon izni verilmedi. Tarayıcının adres çubuğundaki kamera simgesinden izin verip yeniden deneyin.'
const UNSUPPORTED = 'Bu tarayıcı kamera ve mikrofona erişime izin vermiyor. Güncel Chrome, Edge, Firefox ya da Safari kullanın.'

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
    let dispose: (() => void) | undefined

    void (async () => {
      let stream = isStreamLive(streamRef.current) ? streamRef.current : null
      if (!stream) {
        try {
          stream = await openExamStream()
        } catch {
          // İzin reddedildi ya da kamera/mikrofon kullanılamıyor: izlenemeyen sınav sürmemeli (FR-17).
          if (!cancelled) {
            setError(DENIED)
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
      stopMonitor = startMediaMonitor(
        stream,
        {
          onFrame: (frame) => sendFrame(sessionId, frame).catch(onRejected),
          onAudio: (chunk) => sendAudio(sessionId, chunk).catch(onRejected),
          onTrackEnded: () => {
            setError(DENIED)
            void revoke()
          },
        },
        capture,
      )
    })()

    return () => {
      cancelled = true
      stopMonitor?.()
      dispose?.()
    }
  }, [revoke, sessionId, state])

  // Sınav dışında kamera ve mikrofon kapalı (kamera ışığı söner). İzin beklerken alınan yeni akış korunur.
  useEffect(() => {
    if (state === 'InProgress' || state === 'PermissionPending') return
    stopStream(streamRef.current)
    streamRef.current = null
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
      setError(UNSUPPORTED)
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
        setError(caught instanceof ApiError ? caught.message : 'Sınava dönülemedi. Yeniden deneyin.')
      }
      refresh.current()
    } catch {
      setError(DENIED)
    } finally {
      setRequesting(false)
    }
  }, [sessionId])

  return {
    // Gönderim yalnızca InProgress'te; başka durumda akış kapatılmış olur.
    live: live && state === 'InProgress',
    error: supported() ? error : UNSUPPORTED,
    requesting,
    requestPermission,
  }
}
