import { Bookmark, BookmarkCheck, Check, CloudOff } from 'lucide-react'
import { useEffect, useState } from 'react'
import type { SessionQuestion } from '../../api/sessions'
import { OpticalBubble } from '../../components/OpticalBubble'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    count: (current: number, total: number) => `${total} sorudan ${current}.`,
    mark: 'Daha sonra dön',
    queued: (n: number) => `${n} cevap gönderilmeyi bekliyor`,
    saved: 'Kaydedildi',
    clear: 'Seçimi temizle',
  },
  en: {
    count: (current: number, total: number) => `Question ${current} of ${total}`,
    mark: 'Come back later',
    queued: (n: number) => `${n} ${n === 1 ? 'answer' : 'answers'} waiting to be sent`,
    saved: 'Saved',
    clear: 'Clear selection',
  },
})

const LETTERS = 'ABCDEFGHIJ'
/** Kaydedildi işaretinin ekranda kalma süresi. */
const SAVED_FLASH_MS = 1200

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

/**
 * Optik form kâğıdı (K-39, Stitch 14): kenarda büyük soru numarası, harfli baloncuklu seçenekler.
 * Seçenekler radyo grubu; klavyeyle ok tuşlarıyla gezilir. "Kaydedildi" işareti sunucu onayından sonra kısa süre görünür.
 */
export function QuestionCard({ question, index, total, selected, disabled, marked, queued, lastSavedAt, onChoose, onToggleMark }: QuestionCardProps) {
  const m = useMessages(messages)
  const titleId = `question-${question.id}`
  const savedAt = lastSavedAt?.getTime() ?? null
  const [flashFor, setFlashFor] = useState<number | null>(null)

  // Sunucu yeni bir cevabı onayladığında (lastSavedAt değişti) işaret kısa süre görünür.
  const [seenSavedAt, setSeenSavedAt] = useState(savedAt)
  if (savedAt !== seenSavedAt) {
    setSeenSavedAt(savedAt)
    if (savedAt !== null) setFlashFor(savedAt)
  }
  useEffect(() => {
    if (flashFor === null) return
    const timer = window.setTimeout(() => setFlashFor(null), SAVED_FLASH_MS)
    return () => window.clearTimeout(timer)
  }, [flashFor])

  return (
    <section className="question-paper" aria-labelledby={titleId}>
      <p className="question-number" aria-hidden="true">
        {index + 1}
      </p>
      <div className="question-body">
        <header className="question-paper-header">
          <p className="question-count numeric">{m.count(index + 1, total)}</p>
          <button type="button" className={['question-mark', marked && 'is-marked'].filter(Boolean).join(' ')} aria-pressed={marked} onClick={onToggleMark}>
            {marked ? <BookmarkCheck size={16} aria-hidden="true" /> : <Bookmark size={16} aria-hidden="true" />}
            {m.mark}
          </button>
        </header>

        <p id={titleId} className="question-text">
          {question.text}
        </p>

        <div className="question-options" role="radiogroup" aria-labelledby={titleId}>
          {question.options.map((option, optionIndex) => (
            <label key={optionIndex} className="question-option">
              <input type="radio" name={question.id} checked={selected === optionIndex} disabled={disabled} onChange={() => onChoose(optionIndex)} />
              <OpticalBubble filled={selected === optionIndex}>{LETTERS[optionIndex] ?? optionIndex + 1}</OpticalBubble>
              <span className="question-option-text">{option}</span>
            </label>
          ))}
        </div>

        <footer className="question-paper-footer">
          <p className="question-save" aria-live="polite">
            {queued > 0 ? (
              <>
                <CloudOff size={16} aria-hidden="true" />
                {m.queued(queued)}
              </>
            ) : flashFor !== null ? (
              <span className="question-saved" key={flashFor}>
                <span className="question-saved-tick" aria-hidden="true">
                  <Check size={12} strokeWidth={3} />
                </span>
                {m.saved}
              </span>
            ) : null}
          </p>
          {selected != null && !disabled ? (
            <button type="button" className="question-clear" onClick={() => onChoose(null)}>
              {m.clear}
            </button>
          ) : null}
        </footer>
      </div>
    </section>
  )
}
