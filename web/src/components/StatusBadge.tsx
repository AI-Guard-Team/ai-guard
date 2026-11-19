import type { ReactNode } from 'react'

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

interface StatusBadgeProps {
  tone: BadgeTone
  children: ReactNode
}

// Renk tek başına anlam taşımaz; rozetin metni (children) her zaman görünür.
export function StatusBadge({ tone, children }: StatusBadgeProps) {
  return <span className={`badge badge-${tone}`}>{children}</span>
}
