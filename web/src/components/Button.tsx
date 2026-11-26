import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Spinner } from './Spinner'
import './Button.css'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'md' | 'lg'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  block?: boolean
  /** true iken buton kilitlenir ve metnin solunda dönen gösterge çıkar; genişlik değişmez. */
  loading?: boolean
  icon?: ReactNode
  iconEnd?: ReactNode
}

export function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  loading = false,
  icon,
  iconEnd,
  type = 'button',
  className,
  disabled,
  children,
  ...props
}: ButtonProps) {
  const classes = ['button', `button-${variant}`, `button-${size}`, block && 'button-block', className].filter(Boolean).join(' ')

  return (
    <button type={type} className={classes} disabled={disabled || loading} aria-busy={loading || undefined} {...props}>
      {loading ? <Spinner size={16} /> : icon}
      {children}
      {iconEnd}
    </button>
  )
}
