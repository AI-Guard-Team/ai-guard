import { Bookmark, BookmarkCheck, CloudCheck, CloudOff } from 'lucide-react'
import type { SessionQuestion } from '../../api/sessions'

const LETTERS = 'ABCDEFGHIJ'
const TIME = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })

interface QuestionCardProps {
  question: SessionQuestion
  index: number
  total: number
  selected: number | null | undefined
  disabled: boolean
  marked: boolean
  queued: number
  lastSavedAt: Date | null
  onChoose: (option: number | null) => void
  onToggleMark: () => void
}

/** Tek soru ve seçenekleri (Stitch ekran 14). Seçenekler radyo grubu; klavyeyle ok tuşlarıyla gezilir. */
export function QuestionCard({ question, index, total, selected, disabled, marked, queued, lastSavedAt, onChoose, onToggleMark }: QuestionCardProps) {
  const titleId = `question-${question.id}`
  return (
    <section className="question-card" aria-labelledby={titleId}>
      <header className="question-card-header">
        <p className="question-card-count numeric">
          Soru {index + 1} <span>/ {total}</span>
        </p>
        <button type="button" className={['question-mark', marked && 'is-marked'].filter(Boolean).join(' ')} aria-pressed={marked} onClick={onToggleMark}>
          {marked ? <BookmarkCheck size={18} aria-hidden="true" /> : <Bookmark size={18} aria-hidden="true" />}
          Daha sonra dön
        </button>
      </header>

      <p id={titleId} className="question-text">
        {question.text}
      </p>

      <div className="question-options" role="radiogroup" aria-labelledby={titleId}>
        {question.options.map((option, optionIndex) => (
          <label key={optionIndex} className="question-option">
            <input type="radio" name={question.id} checked={selected === optionIndex} disabled={disabled} onChange={() => onChoose(optionIndex)} />
            <span className="question-option-letter" aria-hidden="true">
              {LETTERS[optionIndex] ?? optionIndex + 1}
            </span>
            <span className="question-option-text">{option}</span>
          </label>
        ))}
      </div>

      <footer className="question-card-footer">
        <p className="question-save" aria-live="polite">
          {queued > 0 ? (
            <>
              <CloudOff size={16} aria-hidden="true" />
              {queued} cevap gönderilmeyi bekliyor
            </>
          ) : lastSavedAt ? (
            <>
              <CloudCheck size={16} aria-hidden="true" />
              Cevaplarınız kaydedildi · <time className="numeric">{TIME.format(lastSavedAt)}</time>
            </>
          ) : null}
        </p>
        {selected != null && !disabled ? (
          <button type="button" className="question-clear" onClick={() => onChoose(null)}>
            Seçimi temizle
          </button>
        ) : null}
      </footer>
    </section>
  )
}
