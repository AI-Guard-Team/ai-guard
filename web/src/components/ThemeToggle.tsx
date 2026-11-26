import { Moon, Sun } from 'lucide-react'
import { defineMessages, useMessages } from '../i18n/messages'
import { useTheme } from '../lib/theme'
import './ThemeToggle.css'

const messages = defineMessages({
  tr: { toDark: 'Koyu temaya geç', toLight: 'Açık temaya geç', dark: 'Koyu tema', light: 'Açık tema' },
  en: { toDark: 'Switch to dark theme', toLight: 'Switch to light theme', dark: 'Dark theme', light: 'Light theme' },
})

/** Açık / koyu tema düğmesi (K-42). Tıklayınca o anki temanın tersine geçer ve tercih saklanır. */
export function ThemeToggle({ tone = 'dark' }: { tone?: 'dark' | 'light' }) {
  const m = useMessages(messages)
  const { theme, choose } = useTheme()
  const next = theme === 'dark' ? 'light' : 'dark'
  const label = next === 'dark' ? m.toDark : m.toLight
  return (
    <button type="button" className={`theme-toggle theme-toggle-${tone}`} onClick={() => choose(next)} aria-label={label} title={label}>
      {theme === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
      <span>{theme === 'dark' ? m.light : m.dark}</span>
    </button>
  )
}
