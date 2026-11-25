import type { ReactNode } from 'react'
import './OpticalBubble.css'

interface OpticalBubbleProps {
  children?: ReactNode
  filled?: boolean
  size?: 'sm' | 'md'
  /** Köşede küçük bordo bayrak ("daha sonra dön"). */
  flagged?: boolean
  className?: string
}

/** Optik form baloncuğu (K-38). Yalnızca görünüm; seçim davranışı çevreleyen input/düğmede. */
export function OpticalBubble({ children, filled = false, size = 'md', flagged = false, className }: OpticalBubbleProps) {
  const classes = ['bubble', `bubble-${size}`, filled && 'is-filled', flagged && 'is-flagged', className].filter(Boolean).join(' ')
  return (
    <span className={classes} aria-hidden="true">
      {children}
    </span>
  )
}
