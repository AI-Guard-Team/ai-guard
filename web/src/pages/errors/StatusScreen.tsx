import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { LanguageToggle } from '../../components/LanguageToggle'
import { ThemeToggle } from '../../components/ThemeToggle'
import './StatusScreen.css'

interface StatusScreenProps {
  icon: LucideIcon
  /** "403", "404" gibi kısa kod; ekran okuyucuya başlıkla birlikte okunur. */
  code?: string
  title: string
  children: ReactNode
  actions: ReactNode
  /** true: kabuk dışında tam ekran (404, oturum süresi doldu). false: kabuğun içerik alanında (403). */
  standalone?: boolean
  tone?: 'neutral' | 'danger'
}

/** Hata ve durum ekranlarının ortak düzeni (Stitch ekran 6). */
export function StatusScreen({ icon: Icon, code, title, children, actions, standalone = true, tone = 'neutral' }: StatusScreenProps) {
  const body = (
    <section className={`status-screen status-${tone}`} aria-labelledby="status-title">
      <span className="status-icon" aria-hidden="true">
        <Icon size={28} />
      </span>
      {code ? <p className="status-code numeric">{code}</p> : null}
      <h1 id="status-title">{title}</h1>
      <div className="status-text">{children}</div>
      <div className="status-actions">{actions}</div>
    </section>
  )

  return standalone ? (
    <main className="status-page">
      {/* Kabuk dışında açılan sayfada da dil ve tema seçilebilsin. */}
      <div className="status-prefs">
        <LanguageToggle tone="light" />
        <ThemeToggle tone="light" />
      </div>
      {body}
    </main>
  ) : (
    body
  )
}
