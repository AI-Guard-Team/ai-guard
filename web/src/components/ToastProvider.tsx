import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { ToastContext, type ShowToast, type ToastKind } from './toastContext'
import './Toast.css'

interface ToastItem {
  id: number
  message: string
  kind: ToastKind
}

const VISIBLE_MS = 4000

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(0)
  const timers = useRef<number[]>([])

  useEffect(() => {
    const pending = timers.current
    return () => pending.forEach((timer) => window.clearTimeout(timer))
  }, [])

  const show = useCallback<ShowToast>((message, kind = 'info') => {
    const id = (nextId.current += 1)
    setToasts((current) => [...current, { id, message, kind }])
    const timer = window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id))
    }, VISIBLE_MS)
    timers.current.push(timer)
  }, [])

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="toasts" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.kind}`} role="status">
            {toast.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
