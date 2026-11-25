import type { ReactNode } from 'react'
import './PageHeader.css'

interface PageHeaderProps {
  title: ReactNode
  /** Başlığın üstündeki mono bölüm etiketi (ör. "YÖNETİM / KULLANICILAR"). */
  kicker?: ReactNode
  description?: ReactNode
  actions?: ReactNode
}

/** Her sayfanın başlığı: Fraunces, sayfa başına tek h1. */
export function PageHeader({ title, kicker, description, actions }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div className="page-header-text">
        {kicker ? <p className="kicker">{kicker}</p> : null}
        <h1>{title}</h1>
        {description ? <p className="page-header-description">{description}</p> : null}
      </div>
      {actions ? <div className="page-header-actions">{actions}</div> : null}
    </header>
  )
}
