import { useId, type InputHTMLAttributes } from 'react'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
}

export function Input({ label, error, hint, id, ...props }: InputProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const messageId = `${inputId}-message`

  return (
    <div className="field">
      <label htmlFor={inputId}>{label}</label>
      <input id={inputId} aria-invalid={error ? true : undefined} aria-describedby={error || hint ? messageId : undefined} {...props} />
      {error ? (
        <p id={messageId} className="field-error" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p id={messageId} className="field-hint">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
