import { useEffect, type ReactNode } from 'react'
import { Button } from './Button'
import { defineMessages, useMessages } from '../i18n/messages'
import './Modal.css'

const messages = defineMessages({ tr: { close: 'Kapat' }, en: { close: 'Close' } })

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
}

export function Modal({ open, title, onClose, children }: ModalProps) {
  const m = useMessages(messages)
  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" onClick={(event) => event.stopPropagation()}>
        <header>
          <h2 id="modal-title">{title}</h2>
          <Button variant="ghost" onClick={onClose}>
            {m.close}
          </Button>
        </header>
        {children}
      </div>
    </div>
  )
}
