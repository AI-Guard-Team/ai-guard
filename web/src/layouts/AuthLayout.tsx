import type { ReactNode } from 'react'
import { BrandMark } from '../components/BrandMark'
import { Seal } from '../components/Seal'
import './AuthLayout.css'

interface AuthLayoutProps {
  /** Kartın üstündeki mono etiket (ör. "Giriş"). */
  kicker: string
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}

/**
 * Giriş yapılmadan açılan ekranların ortak düzeni (K-38): solda lacivert panelde iki satırlık ilke ve mühür,
 * sağda krem zemin üzerinde beyaz form kartı. Bütün giriş ekranları aynı yerleşimi kullanır; geçişte kayma olmaz.
 */
export function AuthLayout({ kicker, title, description, children, footer }: AuthLayoutProps) {
  return (
    <div className="auth-layout">
      <aside className="auth-aside">
        <p className="auth-aside-title">
          Adil sınav,
          <br />
          <em>şeffaf karar.</em>
        </p>
        <Seal label="AI‑Guard" ring="AI-GUARD" size={104} className="auth-aside-seal" title="AI-Guard mührü" />
      </aside>

      <main className="auth-main">
        <div className="auth-mobile-brand">
          <BrandMark tone="light" />
        </div>
        <div className="auth-card">
          <p className="kicker">{kicker}</p>
          <h1>{title}</h1>
          {description ? <p className="auth-form-description">{description}</p> : null}
          <div className="auth-form-body">{children}</div>
          {footer ? <div className="auth-form-footer">{footer}</div> : null}
        </div>
      </main>
    </div>
  )
}
