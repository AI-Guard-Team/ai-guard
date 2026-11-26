// Ekranlarda tekrar eden tarih ve belge numarası biçimleri tek yerde (dil desteği gelince yalnızca burası değişir).

const LOCALE = 'tr-TR'

const DATE = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'long', year: 'numeric' })
const DATE_SHORT = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'long' })
const DATE_TIME = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })
const TIME = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' })
const TIME_SECONDS = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit', second: '2-digit' })

const toDate = (value: string | Date) => (typeof value === 'string' ? new Date(value) : value)

export const formatDate = (value: string | Date) => DATE.format(toDate(value))
export const formatDateShort = (value: string | Date) => DATE_SHORT.format(toDate(value))
export const formatDateTime = (value: string | Date) => DATE_TIME.format(toDate(value))
export const formatTime = (value: string | Date) => TIME.format(toDate(value))
export const formatTimeSeconds = (value: string | Date) => TIME_SECONDS.format(toDate(value))

const pad = (value: number) => String(value).padStart(2, '0')

/** Belge etiketlerindeki makine biçimi: "2026-11-11 19:02" (yerel saat). */
export function formatStamp(value: string | Date): string {
  const d = toDate(value)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** "19:02–20:01" gibi aralık; bitiş yoksa yalnızca başlangıç. */
export function formatTimeRange(start: string | Date, end?: string | Date | null): string {
  return end ? `${formatTime(start)}–${formatTime(end)}` : formatTime(start)
}

/**
 * Belge numarası: "AG-<yıl>-<kimliğin ilk 4 karakteri>". Uydurma bir sayaç değil; kaydın kimliğinden türetiliyor,
 * aynı kayıt her ekranda aynı numarayı taşır.
 */
export function docNumber(id: string, at?: string | Date | null): string {
  const year = at ? toDate(at).getFullYear() : new Date().getFullYear()
  return `AG-${year}-${id.replace(/-/g, '').slice(0, 4).toLocaleUpperCase(LOCALE)}`
}

/** Dakika:saniye ("42:18"); saat varsa "1:02:05". */
export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.round(totalSeconds))
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`
}

export const formatNumber = (value: number, digits = 0) =>
  value.toLocaleString(LOCALE, { minimumFractionDigits: digits, maximumFractionDigits: digits })
