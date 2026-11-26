import { Info, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { defineMessages, useMessages } from '../i18n/messages'
import './InfoTip.css'

const messages = defineMessages({ tr: { close: 'Kapat' }, en: { close: 'Close' } })

interface InfoTipProps {
  /** Düğmenin ekran okuyucu adı ve açılan kutunun başlığı (ör. "Bu sayfa hakkında"). */
  title: string
  children: ReactNode
  /** Kutunun hizası. */
  align?: 'start' | 'end'
}

const PANEL_WIDTH = 352
const GAP = 8

/**
 * "i" bilgi düğmesi: sayfayı metinle doldurmadan, isteyene kısa açıklama. Kutu sayfanın en üst katmanında açılır
 * (portal); kart ya da başlık bandı onu kesemez. Esc ya da dışarı tıklama kapatır.
 */
export function InfoTip({ title, children, align = 'start' }: InfoTipProps) {
  const m = useMessages(messages)
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<{ top: number; left: number } | null>(null)
  const button = useRef<HTMLButtonElement>(null)
  const panel = useRef<HTMLDivElement>(null)
  const panelId = useId()

  const place = useCallback(() => {
    const rect = button.current?.getBoundingClientRect()
    if (!rect) return
    const width = Math.min(PANEL_WIDTH, window.innerWidth - 2 * GAP)
    const preferred = align === 'end' ? rect.right - width : rect.left
    setPosition({ top: rect.bottom + GAP, left: Math.max(GAP, Math.min(preferred, window.innerWidth - width - GAP)) })
  }, [align])

  useLayoutEffect(() => {
    if (open) place()
  }, [open, place])

  useEffect(() => {
    if (!open) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        button.current?.focus()
      }
    }
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node
      if (!button.current?.contains(target) && !panel.current?.contains(target)) setOpen(false)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open, place])

  return (
    <span className="infotip">
      <button
        ref={button}
        type="button"
        className="infotip-button"
        aria-label={title}
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
      >
        <Info size={16} aria-hidden="true" />
      </button>
      {createPortal(
        <AnimatePresence>
          {open && position ? (
            <motion.div
              ref={panel}
              id={panelId}
              role="region"
              aria-label={title}
              className="infotip-panel"
              style={{ top: position.top, left: position.left, width: Math.min(PANEL_WIDTH, window.innerWidth - 2 * GAP) }}
              initial={{ opacity: 0, y: -4, scale: 0.98 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -4, scale: 0.98 }}
              transition={{ duration: 0.16, ease: [0.22, 1, 0.36, 1] }}
            >
              <div className="infotip-head">
                <strong>{title}</strong>
                <button type="button" className="infotip-close" aria-label={m.close} onClick={() => setOpen(false)}>
                  <X size={16} aria-hidden="true" />
                </button>
              </div>
              <div className="infotip-body">{children}</div>
            </motion.div>
          ) : null}
        </AnimatePresence>,
        document.body,
      )}
    </span>
  )
}
