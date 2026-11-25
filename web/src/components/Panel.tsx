import type { ReactNode } from 'react'
import './Panel.css'

interface PanelProps {
  title?: string
  description?: string
  /** Başlığın sağındaki rozet veya eylem. */
  aside?: ReactNode
  /** "accent": özet ve bilgi kutuları için kum rengi zemin (DESIGN.md #FBEBD3). */
  tone?: 'default' | 'accent'
  className?: string
  children: ReactNode
  /** Başlık düzeyi; sayfada h1'den sonra h2 gelir. */
  headingLevel?: 2 | 3
}

/** Kart yüzeyi. İç içe kart kullanılmaz; kartın içindeki gruplar çizgiyle ayrılır. */
export function Panel({ title, description, aside, tone = 'default', className, children, headingLevel = 2 }: PanelProps) {
  const Heading = headingLevel === 2 ? 'h2' : 'h3'
  return (
    <section className={['panel', tone === 'accent' && 'panel-accent', className].filter(Boolean).join(' ')}>
      {title ? (
        <header className="panel-header">
          <div>
            <Heading className="panel-title">{title}</Heading>
            {description ? <p className="panel-description">{description}</p> : null}
          </div>
          {aside ? <div className="panel-aside">{aside}</div> : null}
        </header>
      ) : null}
      {children}
    </section>
  )
}
