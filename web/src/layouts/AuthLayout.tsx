import { FileCheck2, ShieldCheck, UserCheck } from 'lucide-react'
import { motion } from 'motion/react'
import type { ReactNode } from 'react'
import { BrandMark } from '../components/BrandMark'
import { ThemeToggle } from '../components/ThemeToggle'
import './AuthLayout.css'

// Sol paneldeki ilkeler platformun gerçek davranışını anlatır; her biri bir karara dayanır (K-26, K-30, K-31, K-32).
const PRINCIPLES = [
  { icon: UserCheck, title: 'Kararı insan verir', text: 'Sistem kopya kararı vermez; şüpheli oturumu yetkili değerlendirici inceler.' },
  { icon: ShieldCheck, title: 'Sürekli kayıt yok', text: 'Kamera ve ses anlık analiz edilir; yalnızca şüpheli anların kanıtı saklanır.' },
  { icon: FileCheck2, title: 'Verileriniz korunur', text: 'Kanıtlar şifreli tutulur, saklama süresi dolunca silinir. Biyometri yalnızca açık rızayla.' },
] as const

interface AuthLayoutProps {
  /** @deprecated Eski başlık üstü etiket; yeni tasarımda gösterilmiyor. */
  kicker?: string
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}

/**
 * Giriş yapılmadan açılan ekranların ortak düzeni (K-38): solda lacivert tanıtım paneli, sağda beyaz form kartı.
 * Bütün giriş ekranları aynı yerleşimi kullanır; geçişte kayma olmaz.
 */
export function AuthLayout({ title, description, children, footer }: AuthLayoutProps) {
  return (
    <div className="auth-layout">
      <aside className="auth-aside">
        <BrandMark subtitle="Çevrim içi sınav güvenliği" />
        <div className="auth-aside-body">
          <motion.h2
            className="auth-aside-title"
            initial={{ opacity: 0, y: 14 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          >
            Adil sınav,
            <br />
            <span className="auth-aside-accent">şeffaf karar.</span>
          </motion.h2>
          <ul className="auth-principles">
            {PRINCIPLES.map(({ icon: Icon, title: itemTitle, text }, index) => (
              <motion.li
                key={itemTitle}
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.45, delay: 0.15 + index * 0.08, ease: [0.22, 1, 0.36, 1] }}
              >
                <span className="auth-principle-icon" aria-hidden="true">
                  <Icon size={20} />
                </span>
                <span>
                  <strong>{itemTitle}</strong>
                  <span>{text}</span>
                </span>
              </motion.li>
            ))}
          </ul>
        </div>
        <p className="auth-aside-foot">Doğuş Üniversitesi · YM 491 Araştırma Projesi</p>
      </aside>

      <main className="auth-main">
        <div className="auth-theme">
          <ThemeToggle tone="light" />
        </div>
        <div className="auth-mobile-brand">
          <BrandMark tone="light" />
        </div>
        <div className="auth-card">
          <h1>{title}</h1>
          {description ? <p className="auth-form-description">{description}</p> : null}
          <div className="auth-form-body">{children}</div>
          {footer ? <div className="auth-form-footer">{footer}</div> : null}
        </div>
      </main>
    </div>
  )
}
