import { useId, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import './Input.css'

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
  const [visible, setVisible] = useState(false)
  return (
    <Input
      {...props}
      type={visible ? 'text' : 'password'}
      trailing={
        <button
          type="button"
          className="field-toggle"
          aria-pressed={visible}
          aria-label={visible ? `${props.label}: gizle` : `${props.label}: göster`}
          onClick={() => setVisible((current) => !current)}
        >
          {visible ? 'Gizle' : 'Göster'}
        </button>
      }
    />
  )
}
