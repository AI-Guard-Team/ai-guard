import { useEffect, useRef, useState, type ReactNode } from 'react'
import './HoldToConfirm.css'

interface HoldToConfirmProps {
  /** Basılı tutma süresi (ms). */
  duration?: number
  disabled?: boolean
  onConfirm: () => void
  children: ReactNode
  tone?: 'primary' | 'success' | 'warning' | 'danger'
}

/**
 * Basılı tutarak onaylanan düğme: yanlışlıkla tek tıkla geri alınamaz işlem yapılmasın (ör. karar).
 * Fare, dokunma ve klavye (Boşluk/Enter basılı) ile çalışır; çevresindeki halka dolar.
 */
export function HoldToConfirm({ duration = 1000, disabled = false, onConfirm, children, tone = 'primary' }: HoldToConfirmProps) {
  const [holding, setHolding] = useState(false)
  const timer = useRef<number | null>(null)

  const cancel = () => {
    if (timer.current !== null) window.clearTimeout(timer.current)
    timer.current = null
    setHolding(false)
  }

  const start = () => {
    if (disabled || timer.current !== null) return
    setHolding(true)
    timer.current = window.setTimeout(() => {
      timer.current = null
      setHolding(false)
      onConfirm()
    }, duration)
  }

  useEffect(() => () => {
    if (timer.current !== null) window.clearTimeout(timer.current)
  }, [])

  return (
    <button
      type="button"
      className={['hold-confirm', `hold-confirm-${tone}`, holding && 'is-holding'].filter(Boolean).join(' ')}
      style={{ ['--hold-duration' as string]: `${duration}ms` }}
      disabled={disabled}
      onPointerDown={start}
      onPointerUp={cancel}
      onPointerLeave={cancel}
      onPointerCancel={cancel}
      onKeyDown={(event) => {
        if ((event.key === ' ' || event.key === 'Enter') && !event.repeat) {
          event.preventDefault()
          start()
        }
      }}
      onKeyUp={(event) => {
        if (event.key === ' ' || event.key === 'Enter') cancel()
      }}
      onBlur={cancel}
    >
      <svg className="hold-confirm-ring" viewBox="0 0 36 36" aria-hidden="true">
        <circle cx="18" cy="18" r="15" />
        <circle className="hold-confirm-progress" cx="18" cy="18" r="15" pathLength="100" />
      </svg>
      <span>{children}</span>
    </button>
  )
}
