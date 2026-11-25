import { useId, type CSSProperties, type ReactNode } from 'react'
import './Seal.css'

export type SealTone = 'seal' | 'success' | 'warning' | 'danger'

interface SealProps {
  /** Ortadaki kısa yazı (ör. "RIZA", "TEMİZ"). */
  label: string
  /** Kenardaki yazı halkası; çevreyi dolduracak kadar tekrar eder. */
  ring?: string
  tone?: SealTone
  /** stamped: basılı mühür. empty: boş yuva (kesikli). pending: beklemede (kehribar kesikli). */
  state?: 'stamped' | 'empty' | 'pending'
  /** true iken mühür "basılır" (ölçek + dönme); yalnızca yeni basıldığında verilir. */
  animate?: boolean
  size?: number
  /** Hafif eğim (derece). */
  tilt?: number
  /** Ortada yazının üstünde küçük ikon. */
  icon?: ReactNode
  /** Ekran okuyucu metni; verilmezse `label`. */
  title?: string
  className?: string
}

/**
 * Dijital mühür (K-38): daire, kenarında yazı halkası. Tamamlanan adımlarda ve kararlarda basılır.
 * Renk tek başına anlam taşımaz; ortadaki yazı her zaman okunur.
 */
export function Seal({ label, ring, tone = 'seal', state = 'stamped', animate = false, size = 96, tilt = -6, icon, title, className }: SealProps) {
  const pathId = `seal-ring-${useId().replace(/[^a-zA-Z0-9-]/g, '')}`
  const ringText = ring ? `${ring} • `.repeat(Math.max(1, Math.ceil(34 / (ring.length + 3)))) : ''
  const classes = ['seal', `seal-${tone}`, `is-${state}`, animate && state === 'stamped' && 'is-animated', className]
    .filter(Boolean)
    .join(' ')
  const style = { width: size, height: size, '--seal-tilt': `${tilt}deg`, fontSize: size / 8 } as CSSProperties

  return (
    <span className={classes} style={style} role="img" aria-label={title ?? label}>
      <svg viewBox="0 0 120 120" aria-hidden="true" focusable="false">
        <circle className="seal-outer" cx="60" cy="60" r="56" />
        {state === 'stamped' ? <circle className="seal-inner" cx="60" cy="60" r="40" /> : null}
        {state === 'stamped' && ringText ? (
          <>
            <path id={pathId} d="M60,60 m-48,0 a48,48 0 1,1 96,0 a48,48 0 1,1 -96,0" fill="none" />
            <text className="seal-ring">
              <textPath href={`#${pathId}`} textLength="296" lengthAdjust="spacingAndGlyphs">
                {ringText}
              </textPath>
            </text>
          </>
        ) : null}
      </svg>
      <span className="seal-center" aria-hidden="true">
        {icon && state === 'stamped' ? <span className="seal-icon">{icon}</span> : null}
        <span className="seal-label">{label}</span>
      </span>
    </span>
  )
}
