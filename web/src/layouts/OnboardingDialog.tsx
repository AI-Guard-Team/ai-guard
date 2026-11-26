import { BookOpen, Camera, ChartColumn, Clock3, FolderOpen, Gavel, Info, ListChecks, Scale, Settings2, ShieldCheck, Sparkles, Users, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useEffect, useRef, useState } from 'react'
import type { Role } from '../auth/roles'
import { Button } from '../components/Button'
import { defineMessages, useMessages } from '../i18n/messages'
import { ONBOARDING, type OnboardingStep } from './onboardingSteps'
import './OnboardingDialog.css'

const ICONS: Record<OnboardingStep['icon'], typeof ListChecks> = {
  list: ListChecks,
  camera: Camera,
  shield: ShieldCheck,
  clock: Clock3,
  folder: FolderOpen,
  chart: ChartColumn,
  gavel: Gavel,
  users: Users,
  settings: Settings2,
  book: BookOpen,
  spark: Sparkles,
  scale: Scale,
  info: Info,
}

const messages = defineMessages({
  tr: {
    title: {
      Candidate: 'Aday portalına hoş geldiniz',
      Reviewer: 'Değerlendirici paneline hoş geldiniz',
      Admin: 'Yönetim paneline hoş geldiniz',
    } as Record<Role, string>,
    close: 'Tanıtımı kapat',
    dontShow: 'Bir daha gösterme',
    stepOf: (current: number, total: number) => `Adım ${current} / ${total}`,
    stepLabel: (n: number, title: string) => `${n}. adım: ${title}`,
    back: 'Geri',
    skip: 'Atla',
    next: 'İleri',
    start: 'Başlayalım',
  },
  en: {
    title: {
      Candidate: 'Welcome to the candidate portal',
      Reviewer: 'Welcome to the reviewer workspace',
      Admin: 'Welcome to administration',
    },
    close: 'Close the tour',
    dontShow: 'Don’t show this again',
    stepOf: (current: number, total: number) => `Step ${current} of ${total}`,
    stepLabel: (n: number, title: string) => `Step ${n}: ${title}`,
    back: 'Back',
    skip: 'Skip',
    next: 'Next',
    start: 'Let’s begin',
  },
})

interface OnboardingDialogProps {
  role: Role
  open: boolean
  /** `dontShowAgain`: kullanıcı "Bir daha gösterme"yi seçti. */
  onClose: (dontShowAgain: boolean) => void
}

/**
 * İlk girişte rol bazlı tanıtım: dört kısa adım, ileri/geri, istenince atlanır. Kabuktaki "Tanıtımı göster" ile
 * yeniden açılır. Odak pencereye alınır, Esc kapatır; adım geçişi hareket azaltma tercihinde anında olur.
 */
export function OnboardingDialog({ role, open, onClose }: OnboardingDialogProps) {
  const m = useMessages(messages)
  const steps = ONBOARDING[role]
  const [index, setIndex] = useState(0)
  const [direction, setDirection] = useState(1)
  const [dontShowAgain, setDontShowAgain] = useState(false)
  const close = () => onClose(dontShowAgain)
  const dialog = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    dialog.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose(false)
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onClose])

  const go = (next: number) => {
    setDirection(next > index ? 1 : -1)
    setIndex(next)
  }

  const step = steps[index]
  const Icon = ICONS[step.icon]
  const last = index === steps.length - 1

  return (
    <AnimatePresence onExitComplete={() => setIndex(0)}>
      {open ? (
        <motion.div className="onboarding-backdrop" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.2 }}>
          <motion.div
            ref={dialog}
            className="onboarding"
            role="dialog"
            aria-modal="true"
            aria-labelledby="onboarding-title"
            tabIndex={-1}
            initial={{ opacity: 0, y: 16, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.98 }}
            transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
          >
            <button type="button" className="onboarding-close" aria-label={m.close} onClick={close}>
              <X size={18} aria-hidden="true" />
            </button>

            <div className="onboarding-visual" aria-hidden="true">
              <AnimatePresence mode="wait" custom={direction}>
                <motion.span
                  key={index}
                  className="onboarding-icon"
                  custom={direction}
                  initial={{ opacity: 0, x: direction * 24, rotate: direction * -6 }}
                  animate={{ opacity: 1, x: 0, rotate: 0 }}
                  exit={{ opacity: 0, x: direction * -24 }}
                  transition={{ duration: 0.26, ease: [0.22, 1, 0.36, 1] }}
                >
                  <Icon size={40} strokeWidth={1.75} />
                </motion.span>
              </AnimatePresence>
            </div>

            <div className="onboarding-body">
              <p className="onboarding-eyebrow" id="onboarding-title">
                {m.title[role]}
              </p>
              <AnimatePresence mode="wait" custom={direction}>
                <motion.div
                  key={index}
                  initial={{ opacity: 0, x: direction * 16 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: direction * -16 }}
                  transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
                >
                  <h2>{step.title}</h2>
                  <p>{step.text}</p>
                </motion.div>
              </AnimatePresence>

              <label className="onboarding-dismiss">
                <input type="checkbox" checked={dontShowAgain} onChange={(event) => setDontShowAgain(event.target.checked)} />
                {m.dontShow}
              </label>
              <div className="onboarding-footer">
                <ol className="onboarding-dots" aria-label={m.stepOf(index + 1, steps.length)}>
                  {steps.map((item, dot) => (
                    <li key={item.title}>
                      <button type="button" aria-label={m.stepLabel(dot + 1, item.title)} aria-current={dot === index ? 'step' : undefined} onClick={() => go(dot)} />
                    </li>
                  ))}
                </ol>
                <div className="onboarding-actions">
                  {index > 0 ? (
                    <Button variant="ghost" onClick={() => go(index - 1)}>
                      {m.back}
                    </Button>
                  ) : (
                    <Button variant="ghost" onClick={close}>
                      {m.skip}
                    </Button>
                  )}
                  <Button onClick={() => (last ? close() : go(index + 1))}>{last ? m.start : m.next}</Button>
                </div>
              </div>
            </div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
