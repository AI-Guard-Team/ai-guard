import { ChevronRight } from 'lucide-react'
import { Fragment, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { SECTION_ICON } from '../layouts/sections'
import { IconTile } from './IconTile'
import { InfoTip } from './InfoTip'
import './PageHeader.css'

export interface Crumb {
  label: string
  to?: string
}

interface PageHeaderProps {
  title: ReactNode
  /** Yol çubuğu: kullanıcı nerede olduğunu görsün (ör. Değerlendirici › Sınavlarım). Son öğe bu sayfa. */
  breadcrumbs?: Crumb[]
  /** @deprecated Eski adı; tek öğeli yol çubuğu olarak gösterilir. */
  kicker?: string
  /** Başlığın altında tek cümle: bu sayfada ne yapılır. */
  description?: ReactNode
  /** "i" düğmesinde açılan ayrıntılı açıklama. */
  info?: ReactNode
  actions?: ReactNode
  /** Simgesi gösterilecek bölümün menü adresi; verilmezse adresten bulunur (ör. /admin/exams/... → Sınavlar). */
  section?: string
}

function sectionOf(pathname: string): string | undefined {
  return Object.keys(SECTION_ICON)
    .filter((key) => pathname === key || pathname.startsWith(`${key}/`))
    .sort((a, b) => b.length - a.length)[0]
}

/**
 * Her sayfanın başlık şablonu (K-39): bölümün renkli simgesi, yol çubuğu, başlık + bilgi düğmesi, tek cümle açıklama
 * ve eylemler. Bütün sayfalar aynı düzende başlar; kullanıcı bölümü rengiyle de tanır. Sayfa başına tek h1.
 */
export function PageHeader({ title, breadcrumbs, kicker, description, info, actions, section }: PageHeaderProps) {
  const { pathname } = useLocation()
  const crumbs = breadcrumbs ?? (kicker ? [{ label: kicker }] : [])
  const icon = SECTION_ICON[section ?? sectionOf(pathname) ?? '']

  return (
    <header className="page-header">
      {icon ? <IconTile icon={icon.icon} tone={icon.tone} size="lg" /> : null}
      <div className="page-header-text">
        {crumbs.length ? (
          <nav aria-label="Konumunuz" className="breadcrumbs">
            <ol>
              {crumbs.map((crumb, index) => (
                <Fragment key={`${crumb.label}-${index}`}>
                  <li>{crumb.to ? <Link to={crumb.to}>{crumb.label}</Link> : <span>{crumb.label}</span>}</li>
                  <li aria-hidden="true" className="breadcrumbs-sep">
                    <ChevronRight size={14} />
                  </li>
                </Fragment>
              ))}
              <li aria-current="page" className="breadcrumbs-current">
                {typeof title === 'string' ? title : 'Bu sayfa'}
              </li>
            </ol>
          </nav>
        ) : null}
        <div className="page-header-title">
          <h1>{title}</h1>
          {info ? (
            <InfoTip title="Bu sayfa hakkında" align="start">
              {info}
            </InfoTip>
          ) : null}
        </div>
        {description ? <p className="page-header-description">{description}</p> : null}
      </div>
      {actions ? <div className="page-header-actions">{actions}</div> : null}
    </header>
  )
}
