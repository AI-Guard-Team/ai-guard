import type { InputHTMLAttributes, ReactNode } from 'react'
import './Checkbox.css'

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode
  description?: ReactNode
}

/** Onay kutusu ve etiketi; etiketin tamamı tıklanabilir. Rıza onaylarında kullanılır. */
export function Checkbox({ label, description, className, ...props }: CheckboxProps) {
  return (
    <label className={['checkbox', className].filter(Boolean).join(' ')}>
      <input type="checkbox" {...props} />
      <span className="checkbox-text">
        <span className="checkbox-label">{label}</span>
        {description ? <span className="checkbox-description">{description}</span> : null}
      </span>
    </label>
  )
}
