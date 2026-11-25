import { FileCheck2, ShieldCheck, UserCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import { BrandMark } from '../components/BrandMark'
import './AuthLayout.css'

// Sol paneldeki ilkeler platformun gerçek davranışını anlatır; her biri bir karara dayanır.
const PRINCIPLES = [
  {
    icon: ShieldCheck,
    title: 'Sürekli kayıt tutulmaz',
    text: 'Kamera ve ses yalnızca anlık analiz edilir; yalnızca şüpheli anların kanıt kareleri saklanır.',
  },
  {
    icon: UserCheck,
    title: 'Kararı insan verir',
    text: 'Sistem kopya kararı vermez; şüpheli oturumu yetkili değerlendirici inceler.',
  },
  {
    icon: FileCheck2,
    title: 'Verileriniz korunur',
    text: 'Kanıtlar şifreli saklanır, saklama süresi dolunca silinir. Biyometrik işlem yalnızca açık rızanızla yapılır.',
  },
] as const

interface AuthLayoutProps {
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}

/**
 * Giriş yapılmadan açılan ekranların ortak düzeni: solda lacivert tanıtım paneli,
 * sağda fildişi zemin üzerinde form. Bütün giriş ekranları aynı yerleşimi kullanır; sayfa geçişinde kayma olmaz.
 */
export function AuthLayout({ title, description, children, footer }: AuthLayoutProps) {
  return (
    <div className="auth-layout">
      <aside className="auth-aside">
        <BrandMark subtitle="Çevrim içi sınav güvenliği" />
        <div className="auth-aside-body">
          <p className="auth-aside-title">Adil, şeffaf ve gizliliğe saygılı çevrim içi sınav</p>
          <ul className="auth-principles">
            {PRINCIPLES.map(({ icon: Icon, title: itemTitle, text }) => (
              <li key={itemTitle}>
                <span className="auth-principle-icon" aria-hidden="true">
                  <Icon size={18} />
                </span>
                <span>
                  <strong>{itemTitle}</strong>
                  <span>{text}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
        <p className="auth-aside-foot">Doğuş Üniversitesi · YM 491 Araştırma Projesi</p>
      </aside>

      <main className="auth-main">
        <div className="auth-mobile-brand">
          <BrandMark tone="light" />
        </div>
        <div className="auth-form">
          <h1>{title}</h1>
          {description ? <p className="auth-form-description">{description}</p> : null}
          <div className="auth-form-body">{children}</div>
          {footer ? <div className="auth-form-footer">{footer}</div> : null}
        </div>
      </main>
    </div>
  )
}
