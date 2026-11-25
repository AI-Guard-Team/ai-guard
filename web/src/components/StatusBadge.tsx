import type { ReactNode } from 'react'
import './StatusBadge.css'

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

interface StatusBadgeProps {
  tone: BadgeTone
  children: ReactNode
  /** Rengin yanında küçük bir nokta; durum renkle değil metinle de okunur. */
  dot?: boolean
}

// Renk tek başına anlam taşımaz; rozetin metni (children) her zaman görünür.
export function StatusBadge({ tone, children, dot = false }: StatusBadgeProps) {
  return (
    <span className={`badge badge-${tone}`}>
      {dot ? <span className="badge-dot" aria-hidden="true" /> : null}
      {children}
    </span>
  )
}
