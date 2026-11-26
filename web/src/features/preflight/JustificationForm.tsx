import { useId, useState, type FormEvent } from 'react'
import { Button } from '../../components/Button'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: { required: 'Devam etmek için gerekçe yazın.', short: 'Gerekçe en az 10 karakter olmalı; ne olduğunu kısaca anlatın.', label: 'Gerekçe' },
  en: { required: 'Write a justification to continue.', short: 'The justification must be at least 10 characters; briefly explain what happened.', label: 'Justification' },
})

/** Tek kelimelik ("yok", "x") gerekçe değerlendiriciye bilgi vermez. */
const MIN_REASON = 10

interface JustificationFormProps {
  title: string
  description: string
  submitLabel: string
  pending: boolean
  onSubmit: (reason: string) => void
}

const MAX_REASON = 2000

/** Gerekçeyle devam (K-25): gerekçe oturuma yazılır, oturum incelemeye işaretlenir. */
export function JustificationForm({ title, description, submitLabel, pending, onSubmit }: JustificationFormProps) {
  const m = useMessages(messages)
  const id = useId()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string>()

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const value = reason.trim()
    if (!value) {
      setError(m.required)
      return
    }
    if (value.length < MIN_REASON) {
      setError(m.short)
      return
    }
    setError(undefined)
    onSubmit(value)
  }

  return (
    <form className="justification" onSubmit={submit} noValidate>
      <h3>{title}</h3>
      <p className="field-hint">{description}</p>
      <div className={['field', error && 'has-error'].filter(Boolean).join(' ')}>
        <label htmlFor={id}>{m.label}</label>
        <textarea
          id={id}
          rows={3}
          maxLength={MAX_REASON}
          value={reason}
          aria-invalid={error ? true : undefined}
          aria-describedby={`${id}-hint`}
          onChange={(event) => {
            setReason(event.target.value)
            if (error) setError(undefined)
          }}
        />
        {error ? (
          <p id={`${id}-hint`} className="field-error" role="alert">
            {error}
          </p>
        ) : (
          <p id={`${id}-hint`} className="field-hint numeric">
            {reason.length} / {MAX_REASON}
          </p>
        )}
      </div>
      <Button type="submit" variant="secondary" loading={pending}>
        {submitLabel}
      </Button>
    </form>
  )
}
