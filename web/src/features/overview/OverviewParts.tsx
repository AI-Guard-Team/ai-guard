import { ArrowRight, type LucideIcon } from 'lucide-react'
import { motion } from 'motion/react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { IconTile, type TileTone } from '../../components/IconTile'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({ tr: { all: 'Tümünü gör' }, en: { all: 'View all' } })

/** Karşılama bandı: lacivert, ad ve rolün bu sayfadaki işi. */
export function Hero({ greeting, text, aside }: { greeting: string; text: ReactNode; aside?: ReactNode }) {
  return (
    <motion.section className="hero" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.45, ease: [0.22, 1, 0.36, 1] }}>
      <div className="hero-text">
        <h1>{greeting}</h1>
        <p>{text}</p>
      </div>
      {aside ? <div className="hero-aside">{aside}</div> : null}
    </motion.section>
  )
}

interface StatTileProps {
  icon: LucideIcon
  tone: TileTone
  value: ReactNode
  label: string
  hint?: string
  to?: string
  index?: number
}

/** Tek bir ölçü: renkli simge, büyük sayı, kısa ad. Bağlantılıysa ilgili sayfayı açar. */
export function StatTile({ icon, tone, value, label, hint, to, index = 0 }: StatTileProps) {
  const body = (
    <>
      <IconTile icon={icon} tone={tone} />
      <span className="stat-tile-text">
        <span className="stat-tile-value numeric">{value}</span>
        <span className="stat-tile-label">{label}</span>
        {hint ? <span className="stat-tile-hint">{hint}</span> : null}
      </span>
    </>
  )
  return (
    <motion.li initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35, delay: 0.08 + index * 0.05, ease: [0.22, 1, 0.36, 1] }}>
      {to ? (
        <Link className="stat-tile is-link" to={to}>
          {body}
        </Link>
      ) : (
        <div className="stat-tile">{body}</div>
      )}
    </motion.li>
  )
}

/** Kart başlığı + "tümünü gör" bağlantısı. */
export function CardHead({ title, to, linkLabel }: { title: string; to?: string; linkLabel?: string }) {
  const m = useMessages(messages)
  return (
    <div className="overview-card-head">
      <h2 className="overview-card-title">{title}</h2>
      {to ? (
        <Link className="overview-link" to={to}>
          {linkLabel ?? m.all}
          <ArrowRight size={16} aria-hidden="true" />
        </Link>
      ) : null}
    </div>
  )
}
