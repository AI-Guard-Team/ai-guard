import { BookOpen, Camera, ChartColumn, Clock3, FolderOpen, Gavel, ListChecks, Settings2, ShieldCheck, Users, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useEffect, useRef, useState } from 'react'
import type { Role } from '../auth/roles'
import { Button } from '../components/Button'
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
}

const ROLE_TITLE: Record<Role, string> = {
  Candidate: 'Aday portalına hoş geldiniz',
  Reviewer: 'Değerlendirici paneline hoş geldiniz',
  Admin: 'Yönetim paneline hoş geldiniz',
}

interface OnboardingDialogProps {
  role: Role
  open: boolean
  onClose: () => void
}

/**
 * İlk girişte rol bazlı tanıtım: dört kısa adım, ileri/geri, istenince atlanır. Kabuktaki "Tanıtımı göster" ile
 * yeniden açılır. Odak pencereye alınır, Esc kapatır; adım geçişi hareket azaltma tercihinde anında olur.
 */
export function OnboardingDialog({ role, open, onClose }: OnboardingDialogProps) {
  const steps = ONBOARDING[role]
  const [index, setIndex] = useState(0)
  const [direction, setDirection] = useState(1)
  const dialog = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    dialog.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
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
            <button type="button" className="onboarding-close" aria-label="Tanıtımı kapat" onClick={onClose}>
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
                {ROLE_TITLE[role]}
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

              <div className="onboarding-footer">
                <ol className="onboarding-dots" aria-label={`Adım ${index + 1} / ${steps.length}`}>
                  {steps.map((item, dot) => (
                    <li key={item.title}>
                      <button type="button" aria-label={`${dot + 1}. adım: ${item.title}`} aria-current={dot === index ? 'step' : undefined} onClick={() => go(dot)} />
                    </li>
                  ))}
                </ol>
                <div className="onboarding-actions">
                  {index > 0 ? (
                    <Button variant="ghost" onClick={() => go(index - 1)}>
                      Geri
                    </Button>
                  ) : (
                    <Button variant="ghost" onClick={onClose}>
                      Atla
                    </Button>
                  )}
                  <Button onClick={() => (last ? onClose() : go(index + 1))}>{last ? 'Başlayalım' : 'İleri'}</Button>
                </div>
              </div>
            </div>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
