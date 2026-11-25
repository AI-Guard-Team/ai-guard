import type { LucideIcon } from 'lucide-react'
import { motion } from 'motion/react'
import type { ReactNode } from 'react'
import { IconTile, type TileTone } from './IconTile'
import './ActionBanner.css'

interface ActionBannerProps {
  icon: LucideIcon
  tone?: TileTone
  title: string
  children: ReactNode
  /** Sağdaki eylem (bağlantı ya da düğme). */
  action?: ReactNode
}

/** Yapılması gereken bir iş: renkli simge, kısa başlık, tek cümle ve eylem. Uyarı metni yerine tasarlanmış kart. */
export function ActionBanner({ icon, tone = 'rose', title, children, action }: ActionBannerProps) {
  return (
    <motion.section className={`action-banner action-banner-${tone}`} aria-label={title} initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}>
      <IconTile icon={icon} tone={tone} />
      <div className="action-banner-text">
        <strong>{title}</strong>
        <span>{children}</span>
      </div>
      {action ? <div className="action-banner-action">{action}</div> : null}
    </motion.section>
  )
}
