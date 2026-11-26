import { useState } from 'react'
import { decide, type ReviewDecisionType } from '../../api/review'
import { ApiError } from '../../api/client'
import { Alert } from '../../components/Alert'
import { HoldToConfirm } from '../../components/HoldToConfirm'
import { Seal } from '../../components/Seal'
import { DECISION_LABEL, DECISION_TONE } from '../../lib/eventCatalog'

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
  const [choice, setChoice] = useState<ReviewDecisionType | null>(null)
  const [note, setNote] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const confirm = async () => {
    if (!choice) return
    setPending(true)
    setError(null)
    try {
      await decide(sessionId, choice, note.trim() || null)
      onDecided()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Karar kaydedilemedi.')
      setPending(false)
    }
  }

  return (
    <div className="decision">
      <fieldset className="decision-options">
        <legend className="visually-hidden">Karar</legend>
        {OPTIONS.map((option) => (
          <label key={option} className={['decision-option', choice === option && 'is-chosen'].filter(Boolean).join(' ')}>
            <input type="radio" name="decision" value={option} checked={choice === option} onChange={() => setChoice(option)} />
            <Seal label={DECISION_LABEL[option]} tone={DECISION_TONE[option]} size={choice === option ? 104 : 84} state={choice === option ? 'stamped' : 'empty'} tilt={0} />
          </label>
        ))}
      </fieldset>

      <label className="kicker" htmlFor={`decision-note-${sessionId}`}>
        Gerekçe (isteğe bağlı)
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
          {pending ? 'Kaydediliyor…' : choice ? `${DECISION_LABEL[choice]}: basılı tut ve onayla` : 'Önce bir karar seçin'}
        </HoldToConfirm>
        <p className="decision-hint">Karar bir kez verilir ve değiştirilemez.</p>
      </div>
    </div>
  )
}
