import { Info, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import './InfoTip.css'

interface InfoTipProps {
  /** Düğmenin ekran okuyucu adı ve açılan kutunun başlığı (ör. "Bu sayfa hakkında"). */
  title: string
  children: ReactNode
  /** Kutunun hizası. */
  align?: 'start' | 'end'
}

/**
 * "i" bilgi düğmesi: sayfayı metinle doldurmadan, isteyene kısa açıklama (PRODUCT.md, yön bulma).
 * Tıklayınca açılır; Esc ya da dışarı tıklama kapatır. Klavyeyle ulaşılır, odak kutuya geçmez.
 */
export function InfoTip({ title, children, align = 'start' }: InfoTipProps) {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLSpanElement>(null)
  const panelId = useId()

  useEffect(() => {
    if (!open) return
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointer)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointer)
    }
  }, [open])

  return (
    <span className="infotip" ref={root}>
      <button type="button" className="infotip-button" aria-label={title} aria-expanded={open} aria-controls={panelId} onClick={() => setOpen((value) => !value)}>
        <Info size={16} aria-hidden="true" />
      </button>
      <AnimatePresence>
        {open ? (
          <motion.div
            id={panelId}
            role="region"
            aria-label={title}
            className={`infotip-panel infotip-${align}`}
            initial={{ opacity: 0, y: -4, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -4, scale: 0.98 }}
            transition={{ duration: 0.16, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="infotip-head">
              <strong>{title}</strong>
              <button type="button" className="infotip-close" aria-label="Kapat" onClick={() => setOpen(false)}>
                <X size={16} aria-hidden="true" />
              </button>
            </div>
            <div className="infotip-body">{children}</div>
          </motion.div>
        ) : null}
      </AnimatePresence>
    </span>
  )
}
