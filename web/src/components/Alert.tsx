import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import './Alert.css'

export type AlertTone = 'info' | 'success' | 'warning' | 'danger'

const ICONS = { info: Info, success: CircleCheck, warning: TriangleAlert, danger: CircleAlert } as const

interface AlertProps {
  tone?: AlertTone
  title?: string
  children?: ReactNode
  /** Hata ve uyarılar ekran okuyucuya hemen okunur; bilgi kutuları okunmaz. */
  live?: boolean
}

export function Alert({ tone = 'info', title, children, live = tone === 'danger' || tone === 'warning' }: AlertProps) {
  const Icon = ICONS[tone]
  return (
    <div className={`alert alert-${tone}`} role={live ? 'alert' : undefined}>
      <Icon className="alert-icon" size={20} aria-hidden="true" />
      <div className="alert-body">
        {title ? <p className="alert-title">{title}</p> : null}
        {children ? <div className="alert-text">{children}</div> : null}
      </div>
    </div>
  )
}

/** Form gönderiminin genel hatası. Mesaj yoksa hiçbir şey çizilmez. */
export function FormAlert({ message }: { message: string | null }) {
  if (!message) return null
  return <Alert tone="danger">{message}</Alert>
}
