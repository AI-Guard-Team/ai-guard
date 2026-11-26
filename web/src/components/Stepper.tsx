import { Check } from 'lucide-react'
import { defineMessages, useMessages } from '../i18n/messages'
import './Stepper.css'

const messages = defineMessages({ tr: { done: ' (tamamlandı)' }, en: { done: ' (completed)' } })

interface StepperProps {
  steps: readonly string[]
  /** Etkin adımın sırası (0'dan). */
  current: number
  label: string
}

/** Çok adımlı akış göstergesi (DESIGN.md "Adım göstergesi"). Tamamlanan adımlar onay işaretiyle. */
export function Stepper({ steps, current, label }: StepperProps) {
  const m = useMessages(messages)
  return (
    <ol className="stepper" aria-label={label}>
      {steps.map((step, index) => {
        const state = index < current ? 'done' : index === current ? 'current' : 'upcoming'
        return (
          <li key={step} className={`stepper-item is-${state}`} aria-current={state === 'current' ? 'step' : undefined}>
            <span className="stepper-marker" aria-hidden="true">
              {state === 'done' ? <Check size={14} strokeWidth={3} /> : index + 1}
            </span>
            <span className="stepper-label">
              {step}
              {state === 'done' ? <span className="visually-hidden">{m.done}</span> : null}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
