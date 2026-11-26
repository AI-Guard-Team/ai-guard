import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import './EmptyState.css'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  children: ReactNode
  action?: ReactNode
}

/** Boş liste ya da yüklenemeyen içerik: ne olduğunu ve ne yapılacağını söyler. */
export function EmptyState({ icon: Icon, title, children, action }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <span className="empty-state-icon" aria-hidden="true">
        <Icon size={24} />
      </span>
      <h2>{title}</h2>
      <div className="empty-state-text">{children}</div>
      {action ? <div className="empty-state-action">{action}</div> : null}
    </div>
  )
}
