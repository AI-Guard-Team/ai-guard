import { useId, useState, type InputHTMLAttributes, type KeyboardEvent, type ReactNode } from 'react'
import { defineMessages, useMessages } from '../i18n/messages'
import './Input.css'

const messages = defineMessages({
  tr: { show: 'Göster', hide: 'Gizle', showLabel: (label: string) => `${label}: göster`, hideLabel: (label: string) => `${label}: gizle`, capsLock: 'Caps Lock açık.' },
  en: { show: 'Show', hide: 'Hide', showLabel: (label: string) => `Show ${label.toLowerCase()}`, hideLabel: (label: string) => `Hide ${label.toLowerCase()}`, capsLock: 'Caps Lock is on.' },
})

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
  /** Alanın solundaki ikon (yalnızca süs; ekran okuyucudan gizlenir). */
  icon?: ReactNode
  /** Alanın sağındaki eylem (ör. şifreyi göster). */
  trailing?: ReactNode
}

export function Input({ label, error, hint, icon, trailing, id, className, ...props }: InputProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const messageId = `${inputId}-message`
  const classes = ['field', error && 'has-error', icon && 'has-icon', trailing && 'has-trailing', className].filter(Boolean).join(' ')

  return (
    <div className={classes}>
      <label htmlFor={inputId}>{label}</label>
      <div className="field-control">
        {icon ? (
          <span className="field-icon" aria-hidden="true">
            {icon}
          </span>
        ) : null}
        <input id={inputId} aria-invalid={error ? true : undefined} aria-describedby={error || hint ? messageId : undefined} {...props} />
        {trailing ? <span className="field-trailing">{trailing}</span> : null}
      </div>
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

type PasswordInputProps = Omit<InputProps, 'type' | 'trailing'>

/**
 * Şifre alanı. "Göster" düğmesi göz ikonu yerine metin: DESIGN.md gözetim çağrıştıran
 * metaforları (göz, kamera) yasaklıyor ve metin ekran okuyucuda da anlaşılır.
 */
export function PasswordInput(props: PasswordInputProps) {
  const m = useMessages(messages)
  const [visible, setVisible] = useState(false)
  // Caps Lock açıkken şifre fark edilmeden yanlış yazılır; kullanıcı alan içindeyken uyarılır.
  const [capsLock, setCapsLock] = useState(false)
  const trackCapsLock = (event: KeyboardEvent<HTMLInputElement>) => setCapsLock(event.getModifierState?.('CapsLock') ?? false)
  return (
    <Input
      {...props}
      type={visible ? 'text' : 'password'}
      hint={capsLock ? m.capsLock : props.hint}
      onKeyDown={(event) => {
        trackCapsLock(event)
        props.onKeyDown?.(event)
      }}
      onKeyUp={(event) => {
        trackCapsLock(event)
        props.onKeyUp?.(event)
      }}
      onBlur={(event) => {
        setCapsLock(false)
        props.onBlur?.(event)
      }}
      trailing={
        <button
          type="button"
          className="field-toggle"
          aria-pressed={visible}
          aria-label={visible ? m.hideLabel(props.label) : m.showLabel(props.label)}
          onClick={() => setVisible((current) => !current)}
        >
          {visible ? m.hide : m.show}
        </button>
      }
    />
  )
}
