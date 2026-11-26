import type { ReactNode } from 'react'
import './DocumentCard.css'

interface DocumentCardProps {
  /** Sol üst köşedeki mono belge numarası (ör. "AG-2026-3F9A"). */
  docNo?: string
  /** Belge numarasının yanındaki mono etiket (ör. "OTURUM 2026-11-11 19:02"). */
  kicker?: ReactNode
  title?: ReactNode
  /** Sağ üst köşe: mühür, rozet ya da eylem. */
  aside?: ReactNode
  tone?: 'default' | 'warning' | 'danger'
  /** Önemli kart: sol kenarda bordo şerit. */
  emphasis?: boolean
  as?: 'section' | 'article' | 'div'
  headingLevel?: 2 | 3
  className?: string
  children?: ReactNode
}

/** Kart (K-39): başlık, altında kısa bilgi satırı (ör. dosya numarası) ve içerik. */
export function DocumentCard({
  docNo,
  kicker,
  title,
  aside,
  tone = 'default',
  emphasis = false,
  as: Tag = 'section',
  headingLevel = 2,
  className,
  children,
}: DocumentCardProps) {
  const Heading = headingLevel === 2 ? 'h2' : 'h3'
  const classes = ['doc-card', `doc-card-${tone}`, emphasis && 'is-emphasis', className].filter(Boolean).join(' ')
  const hasHeader = docNo || kicker || title || aside

  return (
    <Tag className={classes}>
      {hasHeader ? (
        <header className="doc-card-header">
          <div className="doc-card-heading">
            {title ? <Heading className="doc-card-title">{title}</Heading> : null}
            {docNo || kicker ? (
              <p className="kicker doc-card-meta">
                {kicker ? <span>{kicker}</span> : null}
                {docNo ? <span className="doc-card-no">{docNo}</span> : null}
              </p>
            ) : null}
          </div>
          {aside ? <div className="doc-card-aside">{aside}</div> : null}
        </header>
      ) : null}
      {children}
    </Tag>
  )
}
