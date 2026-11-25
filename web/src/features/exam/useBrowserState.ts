import { useEffect, useState } from 'react'

/** Tarayıcının kamera izni (Permissions API). Desteklenmiyorsa null. İzin değişince güncellenir. */
export function useCameraPermission(): PermissionState | null {
  const [permission, setPermission] = useState<PermissionState | null>(null)
  useEffect(() => {
    const permissions = navigator.permissions
    if (!permissions?.query) return
    let current: PermissionStatus | undefined
    let alive = true
    void permissions
      .query({ name: 'camera' as PermissionName })
      .then((status) => {
        if (!alive) return
        current = status
        setPermission(status.state)
        status.onchange = () => setPermission(status.state)
      })
      .catch(() => undefined)
    return () => {
      alive = false
      if (current) current.onchange = null
    }
  }, [])
  return permission
}

/**
 * Tam ekran durumu. Tarayıcı tam ekranı yalnızca kullanıcı tıklamasıyla açıyor; FULLSCREEN_EXIT olayı
 * ancak tam ekrana girilmişse oluşur (S4).
 */
export function useFullscreen() {
  const [fullscreen, setFullscreen] = useState(() => typeof document !== 'undefined' && document.fullscreenElement != null)
  useEffect(() => {
    const onChange = () => setFullscreen(document.fullscreenElement != null)
    document.addEventListener('fullscreenchange', onChange)
    return () => document.removeEventListener('fullscreenchange', onChange)
  }, [])
  const enter = () => {
    void document.documentElement.requestFullscreen?.().catch(() => undefined)
  }
  return { fullscreen, supported: typeof document !== 'undefined' && document.fullscreenEnabled === true, enter }
}
