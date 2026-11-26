import { useEffect, useState } from 'react'
import { getPlatformSettings, type PlatformSettings } from '../api/management'

// Aday ekranlarının okuduğu platform ayarları (K-49): alternatif yöntem metni, destek iletişimi, ajan adresi.
// Sayfa boyunca bir kez çekilir; hata olursa ekranlar kendi genel metinlerine düşer (ayar zorunlu değil).

let cached: Promise<PlatformSettings | null> | null = null

/** Yönetici ayarı kaydedince bir sonraki okuma tazelensin. */
export function invalidatePlatformSettings(): void {
  cached = null
}

export function usePlatformSettings(): PlatformSettings | null {
  const [settings, setSettings] = useState<PlatformSettings | null>(null)
  useEffect(() => {
    let active = true
    cached ??= getPlatformSettings().catch(() => {
      cached = null
      return null
    })
    void cached.then((value) => {
      if (active) setSettings(value)
    })
    return () => {
      active = false
    }
  }, [])
  return settings
}
