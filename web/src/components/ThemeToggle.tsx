import { Moon, Sun } from 'lucide-react'
import { useTheme } from '../lib/theme'
import './ThemeToggle.css'

/** Açık / koyu tema düğmesi (K-42). Tıklayınca o anki temanın tersine geçer ve tercih saklanır. */
export function ThemeToggle({ tone = 'dark' }: { tone?: 'dark' | 'light' }) {
  const { theme, choose } = useTheme()
  const next = theme === 'dark' ? 'light' : 'dark'
  const label = next === 'dark' ? 'Koyu temaya geç' : 'Açık temaya geç'
  return (
    <button type="button" className={`theme-toggle theme-toggle-${tone}`} onClick={() => choose(next)} aria-label={label} title={label}>
      {theme === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
      <span>{theme === 'dark' ? 'Açık tema' : 'Koyu tema'}</span>
    </button>
  )
}
