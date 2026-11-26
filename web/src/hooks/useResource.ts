import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../api/client'

export interface Resource<T> {
  data: T | undefined
  error: string | null
  loading: boolean
  /** Veriyi yeniden ister; ekranda eski veri yenisi gelene kadar kalır (kayma ve yanıp sönme olmasın). */
  reload: () => void
}

interface Settled<T> {
  request: object
  data: T | undefined
  error: string | null
}

/**
 * Bir sayfanın veri isteği: yükleniyor / hata / veri. Sayfa kapanınca ya da yeni istek başlayınca
 * önceki istek iptal edilir (AbortController); geç gelen yanıt yeni verinin üstüne yazılmaz.
 * `load` değiştikçe yeniden yüklenir; çağıran yer `useCallback` ile sabitlemeli.
 * "Yükleniyor" ayrı bir durum değil: son yanıt en son isteğe ait değilse yükleniyordur (ek çizim yok).
 */
export function useResource<T>(load: (signal: AbortSignal) => Promise<T>, fallbackError = 'Bilgiler yüklenemedi.'): Resource<T> {
  const [version, setVersion] = useState(0)
  // Her yeni istek için yeni bir kimlik nesnesi; yanıt hangi isteğe ait, bununla anlaşılır.
  const request = useMemo(() => ({ load, version }), [load, version])
  const [settled, setSettled] = useState<Settled<T> | null>(null)
  const fallback = useRef(fallbackError)

  useEffect(() => {
    const controller = new AbortController()
    request.load(controller.signal).then(
      (value) => {
        if (!controller.signal.aborted) setSettled({ request, data: value, error: null })
      },
      (caught: unknown) => {
        if (controller.signal.aborted) return
        const message = caught instanceof ApiError ? caught.message : fallback.current
        setSettled((previous) => ({ request, data: previous?.data, error: message }))
      },
    )
    return () => controller.abort()
  }, [request])

  const reload = useCallback(() => setVersion((current) => current + 1), [])

  return {
    data: settled?.data,
    error: settled?.request === request ? settled.error : null,
    loading: settled?.request !== request,
    reload,
  }
}
