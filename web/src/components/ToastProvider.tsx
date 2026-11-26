import { CircleAlert, CircleCheck, Info, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { defineMessages, useMessages } from '../i18n/messages'
import { ToastContext, type ShowToast, type ToastKind } from './toastContext'
import './Toast.css'

interface ToastItem {
  id: number
  message: string
  kind: ToastKind
}

const VISIBLE_MS = 4000
const ICONS = { success: CircleCheck, error: CircleAlert, info: Info } as const
const messages = defineMessages({ tr: { close: 'Bildirimi kapat' }, en: { close: 'Dismiss notification' } })

/**
 * Bildirimler: sağdan kayarak gelir, alttaki ince çizgi kalan süreyi gösterir, kapanırken de canlanır.
 * Hata bildirimi ekran okuyucuya hemen okunur (role="alert"), diğerleri sırası gelince.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const m = useMessages(messages)
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(0)
  const timers = useRef(new Map<number, number>())

  useEffect(() => {
    const pending = timers.current
    return () => pending.forEach((timer) => window.clearTimeout(timer))
  }, [])

  const dismiss = useCallback((id: number) => {
    window.clearTimeout(timers.current.get(id))
    timers.current.delete(id)
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const show = useCallback<ShowToast>(
    (message, kind = 'info') => {
      const id = (nextId.current += 1)
      // Aynı anda en fazla üç bildirim: eskisi yerini yenisine bırakır.
      setToasts((current) => [...current.slice(-2), { id, message, kind }])
      timers.current.set(
        id,
        window.setTimeout(() => dismiss(id), VISIBLE_MS),
      )
    },
    [dismiss],
  )

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="toasts" aria-live="polite">
        <AnimatePresence initial={false}>
          {toasts.map((toast) => {
            const Icon = ICONS[toast.kind]
            return (
              <motion.div
                key={toast.id}
                layout
                className={`toast toast-${toast.kind}`}
                role={toast.kind === 'error' ? 'alert' : 'status'}
                initial={{ opacity: 0, x: 48, scale: 0.96 }}
                animate={{ opacity: 1, x: 0, scale: 1 }}
                exit={{ opacity: 0, x: 48, scale: 0.96, transition: { duration: 0.18 } }}
                transition={{ type: 'spring', stiffness: 420, damping: 34 }}
              >
                <Icon className="toast-icon" size={18} aria-hidden="true" />
                <span className="toast-message">{toast.message}</span>
                <button type="button" className="toast-close" aria-label={m.close} onClick={() => dismiss(toast.id)}>
                  <X size={16} aria-hidden="true" />
                </button>
                <span className="toast-timer" aria-hidden="true" style={{ animationDuration: `${VISIBLE_MS}ms` }} />
              </motion.div>
            )
          })}
        </AnimatePresence>
      </div>
    </ToastContext.Provider>
  )
}
