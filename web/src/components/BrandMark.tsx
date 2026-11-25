import { ShieldCheck } from 'lucide-react'
import './BrandMark.css'

interface BrandMarkProps {
  /** "dark": lacivert zemin üzerinde (yan menü, giriş paneli). */
  tone?: 'dark' | 'light'
  subtitle?: string
}

/** Marka: kalkan + onay (DESIGN.md güven metaforu) ve ad. */
export function BrandMark({ tone = 'dark', subtitle }: BrandMarkProps) {
  return (
    <div className={`brand brand-${tone}`}>
      <span className="brand-mark" aria-hidden="true">
        <ShieldCheck size={20} strokeWidth={2.25} />
      </span>
      <span className="brand-text">
        <span className="brand-name">AI-Guard</span>
        {subtitle ? <span className="brand-subtitle">{subtitle}</span> : null}
      </span>
    </div>
  )
}
