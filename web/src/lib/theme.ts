import { useCallback, useEffect, useState } from 'react'

// Tema tercihi (K-41): açık, koyu ya da sistem. Tercih tarayıcıda saklanır; <html data-theme> ile uygulanır.

export type ThemePreference = 'light' | 'dark' | 'system'
export type Theme = 'light' | 'dark'

const KEY = 'aiguard.theme'
const MEDIA = '(prefers-color-scheme: dark)'

function readPreference(): ThemePreference {
  try {
    const value = localStorage.getItem(KEY)
    return value === 'light' || value === 'dark' ? value : 'system'
  } catch {
    return 'system'
  }
}

function systemTheme(): Theme {
  return typeof window !== 'undefined' && window.matchMedia?.(MEDIA).matches ? 'dark' : 'light'
}

export function resolveTheme(preference: ThemePreference): Theme {
  return preference === 'system' ? systemTheme() : preference
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
}

/** Uygulama açılırken bir kez: ilk çizimden önce doğru tema. */
export function initTheme(): void {
  applyTheme(resolveTheme(readPreference()))
}

/** Tema tercihi ve geçişi; "sistem" seçiliyken işletim sistemi değişince tema da değişir. */
export function useTheme() {
  const [preference, setPreference] = useState<ThemePreference>(readPreference)
  const theme = resolveTheme(preference)

  useEffect(() => {
    applyTheme(theme)
    if (preference !== 'system') return
    const media = window.matchMedia?.(MEDIA)
    if (!media) return
    const onChange = () => applyTheme(resolveTheme('system'))
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [preference, theme])

  const choose = useCallback((next: ThemePreference) => {
    setPreference(next)
    try {
      if (next === 'system') localStorage.removeItem(KEY)
      else localStorage.setItem(KEY, next)
    } catch {
      // Depolama kapalıysa tercih yalnızca bu sekmede geçerli.
    }
  }, [])

  return { preference, theme, choose }
}
