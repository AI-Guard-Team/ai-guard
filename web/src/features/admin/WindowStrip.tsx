import { getLocale } from '../../i18n/lang'

/** Ayın kısa adı ("Kas", "Nov"); geçerli dilde. */
const formatMonthShort = (day: Date) => new Intl.DateTimeFormat(getLocale(), { month: 'short' }).format(day).replace('.', '')

const DAY = 86_400_000

interface WindowStripProps {
  start: Date
  end: Date
  latestStart: Date
}

/**
 * Erişim penceresinin küçük takvim şeridi (Prompt G): pencere günleri dolu, son başlangıç anı işaretli.
 * Pencere 14 günden uzunsa şerit pencerenin başından 14 gün gösterir.
 */
export function WindowStrip({ start, end, latestStart }: WindowStripProps) {
  const first = new Date(start)
  first.setHours(0, 0, 0, 0)
  first.setDate(first.getDate() - 1)
  const days = Array.from({ length: 16 }, (_, index) => new Date(first.getTime() + index * DAY))
  const inWindow = (day: Date) => day.getTime() + DAY > start.getTime() && day.getTime() < end.getTime()
  const isLatest = (day: Date) => latestStart.getTime() >= day.getTime() && latestStart.getTime() < day.getTime() + DAY

  return (
    <div className="window-strip" aria-hidden="true">
      {days.map((day) => (
        <span key={day.getTime()} className={['window-day', inWindow(day) && 'is-open', isLatest(day) && 'is-latest'].filter(Boolean).join(' ')}>
          <span className="window-day-num">{day.getDate()}</span>
          <span className="window-day-month">{formatMonthShort(day)}</span>
        </span>
      ))}
    </div>
  )
}
