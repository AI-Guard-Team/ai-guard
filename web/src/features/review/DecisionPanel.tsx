import { useState } from 'react'
import { decide, type ReviewDecisionType } from '../../api/review'
import { ApiError } from '../../api/client'
import { Alert } from '../../components/Alert'
import { HoldToConfirm } from '../../components/HoldToConfirm'
import { Seal } from '../../components/Seal'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { DECISION_LABEL, DECISION_TONE } from '../../lib/eventCatalog'

const messages = defineMessages({
  tr: {
    failed: 'Karar kaydedilemedi.',
    decision: 'Karar',
    note: 'Gerekçe (isteğe bağlı)',
    hold: (label: string) => `${label}: basılı tut ve onayla`,
    choose: 'Önce bir karar seçin',
    hint: 'Karar bir kez verilir ve değiştirilemez.',
  },
  en: {
    failed: 'The decision could not be saved.',
    decision: 'Decision',
    note: 'Justification (optional)',
    hold: (label: string) => `${label}: press and hold to confirm`,
    choose: 'Choose a decision first',
    hint: 'A decision is made once and cannot be changed.',
  },
})

const OPTIONS: ReviewDecisionType[] = ['Clean', 'Suspicious', 'Violation']

interface DecisionPanelProps {
  sessionId: string
  onDecided: () => void
}

/**
 * Karar (Stitch 21, Prompt F): üç mühür seçeneği, seçilen büyür; "Basılı tut ve onayla" 1 sn. Karar bir kez verilir
 * ve değiştirilemez (K-31); bu yüzden tek tıkla değil basılı tutarak onaylanıyor.
 */
export function DecisionPanel({ sessionId, onDecided }: DecisionPanelProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const [choice, setChoice] = useState<ReviewDecisionType | null>(null)
  const [note, setNote] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const confirm = async () => {
    if (!choice || pending) return
    setPending(true)
    setError(null)
    try {
      await decide(sessionId, choice, note.trim() || null)
      onDecided()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : m.failed)
      setPending(false)
    }
  }

  return (
    <div className="decision">
      <fieldset className="decision-options">
        <legend className="visually-hidden">{m.decision}</legend>
        {OPTIONS.map((option) => (
          <label key={option} className={['decision-option', choice === option && 'is-chosen'].filter(Boolean).join(' ')}>
            <input type="radio" name="decision" value={option} checked={choice === option} onChange={() => setChoice(option)} />
            <Seal label={DECISION_LABEL[option]} tone={DECISION_TONE[option]} size={choice === option ? 104 : 84} state={choice === option ? 'stamped' : 'empty'} tilt={0} />
          </label>
        ))}
      </fieldset>

      <label className="kicker" htmlFor={`decision-note-${sessionId}`}>
        {m.note}
      </label>
      <textarea
        id={`decision-note-${sessionId}`}
        className="lined-paper"
        rows={3}
        maxLength={2000}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />

      {error ? <Alert tone="danger">{error}</Alert> : null}

      <div className="decision-confirm">
        <HoldToConfirm tone={choice ? DECISION_TONE[choice] : 'primary'} disabled={!choice || pending} onConfirm={() => void confirm()}>
          {pending ? c.saving : choice ? m.hold(DECISION_LABEL[choice]) : m.choose}
        </HoldToConfirm>
        <p className="decision-hint">{m.hint}</p>
      </div>
    </div>
  )
}
