import { FileCheck2, ShieldCheck, UserCheck } from 'lucide-react'
import { motion } from 'motion/react'
import { useEffect, useRef, useState, type PointerEvent, type ReactNode } from 'react'
import { useLocation } from 'react-router'
import { BrandMark } from '../components/BrandMark'
import { LanguageToggle } from '../components/LanguageToggle'
import { ThemeToggle } from '../components/ThemeToggle'
import { defineMessages, useMessages } from '../i18n/messages'
import { AuthOrbit } from './AuthOrbit'
import './AuthLayout.css'

const PRINCIPLE_ICONS = [UserCheck, ShieldCheck, FileCheck2] as const

// Sol paneldeki ilkeler platformun gerçek davranışını anlatır; her biri bir karara dayanır (K-26, K-30, K-31, K-32).
const messages = defineMessages({
  tr: {
    subtitle: 'Çevrim içi sınav güvenliği',
    titleLead: 'Adil sınav,',
    titleAccent: 'şeffaf karar.',
    principles: [
      { title: 'Kararı insan verir', text: 'Sistem kopya kararı vermez; şüpheli oturumu yetkili değerlendirici inceler.' },
      { title: 'Sürekli kayıt yok', text: 'Kamera ve ses anlık analiz edilir; yalnızca şüpheli anların kanıtı saklanır.' },
      { title: 'Verileriniz korunur', text: 'Kanıtlar şifreli tutulur, saklama süresi dolunca silinir. Biyometri yalnızca açık rızayla.' },
    ],
  },
  en: {
    subtitle: 'Online exam integrity',
    titleLead: 'Fair exams,',
    titleAccent: 'transparent decisions.',
    principles: [
      { title: 'A person decides', text: 'The system never decides on cheating; a qualified reviewer examines any suspicious session.' },
      { title: 'No continuous recording', text: 'Camera and audio are analysed in real time; only evidence of suspicious moments is kept.' },
      { title: 'Your data is protected', text: 'Evidence is encrypted and deleted when its retention period ends. Biometrics only with explicit consent.' },
    ],
  },
})

const EASE = [0.22, 1, 0.36, 1] as const

// Tanıtım paneli uygulama açılışında bir kez canlanır; giriş ekranları arasında geçerken yeniden oynamaz,
// yalnızca form kartı yenilenir (sayfa değişti hissi, ama sol panel yerinde).
let introPlayed = false

interface AuthLayoutProps {
  /** @deprecated Eski başlık üstü etiket; yeni tasarımda gösterilmiyor. */
  kicker?: string
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}

/**
 * Giriş yapılmadan açılan ekranların ortak düzeni (K-39, K-47): solda parlayan halkalı lacivert tanıtım paneli
 * (soldan gelir), sağda form kartı (sağdan gelir). Bütün giriş ekranları aynı yerleşimi kullanır; geçişte kayma olmaz.
 */
export function AuthLayout({ title, description, children, footer }: AuthLayoutProps) {
  const m = useMessages(messages)
  const { pathname } = useLocation()
  // İlk çizimde karar verilir; StrictMode'un çift çizimi de aynı sonucu görür (bayrak etkiden sonra düşer).
  const [playIntro] = useState(() => !introPlayed)
  useEffect(() => {
    introPlayed = true
  }, [])

  // İmleç ışığı: konum CSS değişkenine kare başına bir kez yazılır (requestAnimationFrame); React yeniden çizilmez.
  const aside = useRef<HTMLElement>(null)
  const frame = useRef(0)
  const followPointer = (event: PointerEvent<HTMLElement>) => {
    if (event.pointerType !== 'mouse' || frame.current) return
    const { clientX, clientY } = event
    frame.current = requestAnimationFrame(() => {
      frame.current = 0
      const box = aside.current?.getBoundingClientRect()
      if (!box) return
      aside.current?.style.setProperty('--spot-x', `${clientX - box.left}px`)
      aside.current?.style.setProperty('--spot-y', `${clientY - box.top}px`)
    })
  }
  useEffect(() => () => cancelAnimationFrame(frame.current), [])

  return (
    <div className="auth-layout">
      <aside className="auth-aside" ref={aside} onPointerMove={followPointer}>
        <AuthOrbit />
        <motion.div
          className="auth-aside-brand"
          initial={playIntro ? { opacity: 0, x: -24 } : false}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.6, ease: EASE }}
        >
          <BrandMark subtitle={m.subtitle} />
        </motion.div>
        <div className="auth-aside-body">
          <motion.h2
            className="auth-aside-title"
            initial={playIntro ? { opacity: 0, x: -40, filter: 'blur(8px)' } : false}
            animate={{ opacity: 1, x: 0, filter: 'blur(0px)' }}
            transition={{ duration: 0.8, delay: 0.1, ease: EASE }}
          >
            {m.titleLead}
            <br />
            <span className="auth-aside-accent">{m.titleAccent}</span>
          </motion.h2>
          <ul className="auth-principles">
            {m.principles.map(({ title: itemTitle, text }, index) => {
              const Icon = PRINCIPLE_ICONS[index]
              return (
                <motion.li
                  key={index}
                  initial={playIntro ? { opacity: 0, x: -28 } : false}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.55, delay: 0.35 + index * 0.1, ease: EASE }}
                >
                  <span className="auth-principle-icon" aria-hidden="true">
                    <Icon size={20} />
                  </span>
                  <span>
                    <strong>{itemTitle}</strong>
                    <span>{text}</span>
                  </span>
                </motion.li>
              )
            })}
          </ul>
        </div>
      </aside>

      <main className="auth-main">
        {/* Telefonda sol panel yok; halkaların soluk sürümü kartın arkasında. */}
        <div className="auth-main-orbit">
          <AuthOrbit soft />
        </div>
        <div className="auth-prefs">
          <LanguageToggle tone="light" />
          <ThemeToggle tone="light" />
        </div>
        <div className="auth-mobile-brand">
          <BrandMark tone="light" subtitle={m.subtitle} />
        </div>
        {/* Kart sağdan gelir; her giriş ekranında (giriş, şifremi unuttum, sıfırlama) yeniden. Anahtar adres:
            dil değişince kart yeniden kurulmaz, yazılan bilgiler kaybolmaz. */}
        <motion.div
          key={pathname}
          className="auth-card"
          initial={{ opacity: 0, x: 48, filter: 'blur(6px)' }}
          animate={{ opacity: 1, x: 0, filter: 'blur(0px)' }}
          transition={{ duration: 0.6, delay: playIntro ? 0.2 : 0, ease: EASE }}
        >
          <span className="auth-card-sheen" aria-hidden="true" />
          <h1>{title}</h1>
          {description ? <p className="auth-form-description">{description}</p> : null}
          <motion.div
            className="auth-form-body"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.45, delay: playIntro ? 0.45 : 0.15, ease: EASE }}
          >
            {children}
          </motion.div>
          {footer ? <div className="auth-form-footer">{footer}</div> : null}
        </motion.div>
      </main>
    </div>
  )
}
