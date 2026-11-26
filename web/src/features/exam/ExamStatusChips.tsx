import { defineMessages, useMessages } from '../../i18n/messages'
import type { Chip } from './examChips'

const messages = defineMessages({ tr: { status: 'Sınav durumu' }, en: { status: 'Exam status' } })

/**
 * Sınav sırasında sistemin durumu: küçük nokta + "Kamera Açık" gibi kısa mono yazı. Değer her zaman yazıyla görünür
 * (yalnızca üzerine gelince görünen metin klavyeyle okunamazdı); renk tek başına anlam taşımaz.
 */
export function ExamStatusChips({ chips }: { chips: Chip[] }) {
  const m = useMessages(messages)
  return (
    <ul className="exam-signals" aria-label={m.status}>
      {chips.map((chip) => (
        <li key={chip.label} className={`exam-signal exam-signal-${chip.tone}`}>
          <span className="exam-signal-dot" aria-hidden="true" />
          {chip.label} {chip.value}
        </li>
      ))}
    </ul>
  )
}
