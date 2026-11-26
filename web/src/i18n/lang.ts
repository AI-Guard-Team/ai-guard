import { useSyncExternalStore } from 'react'

// Arayüz dili (K-45): Türkçe ya da İngilizce. Tercih tarayıcıda saklanır; ilk açılışta tarayıcı dili Türkçe değilse İngilizce.
// Tek kaynak bu modül: bileşenler useLang ile, düz modüller getLang ile okur.

export type Lang = 'tr' | 'en'

export const LANGS: readonly Lang[] = ['tr', 'en']

const KEY = 'aiguard.lang'
const LOCALES: Record<Lang, string> = { tr: 'tr-TR', en: 'en-GB' }

function readStored(): Lang | null {
  try {
    const value = localStorage.getItem(KEY)
    return value === 'tr' || value === 'en' ? value : null
  } catch {
    return null
  }
}

function browserLang(): Lang {
  if (typeof navigator === 'undefined') return 'tr'
  const preferred = navigator.languages?.[0] ?? navigator.language ?? 'tr'
  return preferred.toLowerCase().startsWith('tr') ? 'tr' : 'en'
}

let current: Lang = 'tr'
const listeners = new Set<() => void>()

function apply(lang: Lang): void {
  if (typeof document !== 'undefined') document.documentElement.lang = lang
}

/** Uygulama açılırken bir kez: ilk çizimden önce doğru dil. */
export function initLang(): void {
  current = readStored() ?? browserLang()
  apply(current)
}

export function getLang(): Lang {
  return current
}

export function getLocale(lang: Lang = current): string {
  return LOCALES[lang]
}

export function setLang(next: Lang): void {
  if (next === current) return
  current = next
  apply(next)
  try {
    localStorage.setItem(KEY, next)
  } catch {
    // Depolama kapalıysa tercih yalnızca bu sekmede geçerli.
  }
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Geçerli dil; dil değişince bileşen yeniden çizilir. */
export function useLang(): Lang {
  return useSyncExternalStore(subscribe, getLang, getLang)
}
