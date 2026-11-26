import { CircleAlert, CircleCheck, CircleX, ShieldCheck } from 'lucide-react'
import type { CSSProperties, ReactNode } from 'react'
import './Seal.css'

export type SealTone = 'seal' | 'success' | 'warning' | 'danger'

interface SealProps {
  /** Ortadaki kısa yazı (ör. "Rıza", "Temiz"). */
  label: string
  /** Eski sürümde kenar yazısıydı; yeni görünümde kullanılmıyor (geriye uyumluluk). */
  ring?: string
  tone?: SealTone
  /** stamped: tamamlandı/verildi. empty: boş yuva (kesikli). pending: beklemede. */
  state?: 'stamped' | 'empty' | 'pending'
  /** true iken rozet yumuşak bir "yerine oturma" hareketiyle belirir; yalnızca yeni verildiğinde. */
  animate?: boolean
  size?: number
  /** Eski sürümde eğimdi; yeni görünümde kullanılmıyor. */
  tilt?: number
  /** Varsayılan ikonun yerine. */
  icon?: ReactNode
  /** Ekran okuyucu metni; verilmezse `label`. */
  title?: string
  className?: string
}

const ICONS = { seal: ShieldCheck, success: CircleCheck, warning: CircleAlert, danger: CircleX } as const

/**
 * Karar ve tamamlanma rozeti (K-39): yumuşak renkli daire, ortada ikon ve kısa yazı. Karar (Temiz/Şüpheli/İhlal) ve
 * sihirbaz adımlarında kullanılır. Renk tek başına anlam taşımaz; yazı her zaman görünür.
 */
export function Seal({ label, tone = 'seal', state = 'stamped', animate = false, size = 96, icon, title, className }: SealProps) {
  const Icon = ICONS[tone]
  const classes = ['seal', `seal-${tone}`, `is-${state}`, animate && state === 'stamped' && 'is-animated', className]
    .filter(Boolean)
    .join(' ')
  const style = { width: size, height: size, fontSize: Math.max(11, size / 8) } as CSSProperties

  return (
    <span className={classes} style={style} role="img" aria-label={title ?? label}>
      <span className="seal-center" aria-hidden="true">
        {state === 'stamped' ? <span className="seal-icon">{icon ?? <Icon size={Math.round(size / 3.6)} strokeWidth={2} />}</span> : null}
        <span className="seal-label">{label}</span>
      </span>
    </span>
  )
}
