import type { LucideIcon } from 'lucide-react'
import './IconTile.css'

export type TileTone = 'indigo' | 'sky' | 'lilac' | 'rose' | 'mint' | 'amber' | 'slate'

interface IconTileProps {
  icon: LucideIcon
  tone?: TileTone
  size?: 'sm' | 'md' | 'lg'
}

/** Renkli simge kutusu: her bölümün tanınır rengi (menü, sayfa başlığı, genel bakış kartları). Yalnızca süs. */
export function IconTile({ icon: Icon, tone = 'indigo', size = 'md' }: IconTileProps) {
  const iconSize = size === 'sm' ? 17 : size === 'md' ? 22 : 28
  return (
    <span className={`icon-tile icon-tile-${tone} icon-tile-${size}`} aria-hidden="true">
      <Icon size={iconSize} strokeWidth={2} />
    </span>
  )
}
