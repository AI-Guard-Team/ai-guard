import { Languages } from 'lucide-react'
import { setLang, useLang } from '../i18n/lang'
import './ThemeToggle.css'

// Düğme her zaman öbür dilin kendi adıyla yazılır: Türkçe bilmeyen biri "English"i, İngilizce bilmeyen "Türkçe"yi tanır.
const OTHER = {
  tr: { next: 'en', label: 'English', aria: 'Switch to English' },
  en: { next: 'tr', label: 'Türkçe', aria: 'Türkçeye geç' },
} as const

/** Türkçe / İngilizce geçişi (K-46). Tercih saklanır; bütün ekranlar ve tarih biçimleri anında değişir. */
export function LanguageToggle({ tone = 'dark' }: { tone?: 'dark' | 'light' }) {
  const lang = useLang()
  const other = OTHER[lang]
  return (
    <button
      type="button"
      className={`theme-toggle theme-toggle-${tone} lang-toggle`}
      onClick={() => setLang(other.next)}
      aria-label={other.aria}
      title={other.aria}
      lang={other.next}
    >
      <Languages size={18} aria-hidden="true" />
      <span>{other.label}</span>
    </button>
  )
}
