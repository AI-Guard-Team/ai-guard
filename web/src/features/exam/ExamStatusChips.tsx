import type { Chip } from './examChips'

/** Sınav sırasında sistemin durumu; renk tek başına anlam taşımaz, değer yazıyla da var. */
export function ExamStatusChips({ chips }: { chips: Chip[] }) {
  return (
    <ul className="exam-chips" aria-label="Sınav durumu">
      {chips.map((chip) => (
        <li key={chip.label} className={`exam-chip exam-chip-${chip.tone}`}>
          <span className="exam-chip-dot" aria-hidden="true" />
          {chip.label} {chip.value}
        </li>
      ))}
    </ul>
  )
}
