import { getLang, getLocale, type Lang } from '../i18n/lang'

// Ekranlarda tekrar eden tarih ve belge numarası biçimleri tek yerde. Biçimler geçerli dile göre (K-46).

type FormatterName = 'date' | 'dateShort' | 'dateTime' | 'time' | 'timeSeconds'

const OPTIONS: Record<FormatterName, Intl.DateTimeFormatOptions> = {
  date: { day: 'numeric', month: 'long', year: 'numeric' },
  dateShort: { day: 'numeric', month: 'long' },
  dateTime: { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' },
  time: { hour: '2-digit', minute: '2-digit' },
  timeSeconds: { hour: '2-digit', minute: '2-digit', second: '2-digit' },
}

// Intl nesneleri pahalı; dil başına bir kez kurulur.
const cache = new Map<string, Intl.DateTimeFormat>()

function formatter(name: FormatterName, lang: Lang = getLang()): Intl.DateTimeFormat {
  const key = `${lang}:${name}`
  let value = cache.get(key)
  if (!value) {
    value = new Intl.DateTimeFormat(getLocale(lang), OPTIONS[name])
    cache.set(key, value)
  }
  return value
}

const toDate = (value: string | Date) => (typeof value === 'string' ? new Date(value) : value)

export const formatDate = (value: string | Date) => formatter('date').format(toDate(value))
export const formatDateShort = (value: string | Date) => formatter('dateShort').format(toDate(value))
export const formatDateTime = (value: string | Date) => formatter('dateTime').format(toDate(value))
export const formatTime = (value: string | Date) => formatter('time').format(toDate(value))
export const formatTimeSeconds = (value: string | Date) => formatter('timeSeconds').format(toDate(value))

const pad = (value: number) => String(value).padStart(2, '0')

/** Belge etiketlerindeki makine biçimi: "2026-11-11 19:02" (yerel saat); dilden bağımsız. */
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
 * aynı kayıt her ekranda ve her dilde aynı numarayı taşır.
 */
export function docNumber(id: string, at?: string | Date | null): string {
  const year = at ? toDate(at).getFullYear() : new Date().getFullYear()
  return `AG-${year}-${id.replace(/-/g, '').slice(0, 4).toUpperCase()}`
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
  value.toLocaleString(getLocale(), { minimumFractionDigits: digits, maximumFractionDigits: digits })

/** Yüzde: Türkçede "%42", İngilizcede "42%". */
export const formatPercent = (value: number, digits = 0) =>
  new Intl.NumberFormat(getLocale(), { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value / 100)
