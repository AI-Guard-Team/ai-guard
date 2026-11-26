import type { SessionQuestion } from '../../api/sessions'
import { OpticalBubble } from '../../components/OpticalBubble'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    nav: 'Soru gezinmesi',
    sheet: (answered: number, total: number) => `Cevap kâğıdı · ${answered}/${total} cevaplandı`,
    answered: 'cevaplandı',
    blank: 'boş',
    marked: 'daha sonra dönülecek',
    item: (n: number, state: string) => `${n}. soru, ${state}`,
  },
  en: {
    nav: 'Question navigation',
    sheet: (answered: number, total: number) => `Answer sheet · ${answered}/${total} answered`,
    answered: 'answered',
    blank: 'blank',
    marked: 'marked to revisit',
    item: (n: number, state: string) => `Question ${n}, ${state}`,
  },
})

interface QuestionNavigatorProps {
  questions: SessionQuestion[]
  current: number
  selections: Map<string, number | null>
  marked: ReadonlySet<string>
  onSelect: (index: number) => void
}

/**
 * Minyatür optik cevap kâğıdı (K-39): cevaplanan baloncuk dolu, boş olan çerçeveli, işaretli olanın köşesinde
 * bordo bayrak, mevcut soru altı çizili. Durum ekran okuyucuya düğme adıyla söyleniyor.
 */
export function QuestionNavigator({ questions, current, selections, marked, onSelect }: QuestionNavigatorProps) {
  const m = useMessages(messages)
  const answered = questions.filter((question) => selections.get(question.id) != null).length

  return (
    <nav className="answer-sheet" aria-label={m.nav}>
      <p className="kicker numeric">{m.sheet(answered, questions.length)}</p>
      <ol className="answer-sheet-grid">
        {questions.map((question, index) => {
          const isAnswered = selections.get(question.id) != null
          const isMarked = marked.has(question.id)
          const state = [isAnswered ? m.answered : m.blank, isMarked ? m.marked : null].filter(Boolean).join(', ')
          return (
            <li key={question.id}>
              <button
                type="button"
                className="answer-sheet-item"
                aria-current={index === current ? 'true' : undefined}
                aria-label={m.item(index + 1, state)}
                onClick={() => onSelect(index)}
              >
                <OpticalBubble size="sm" filled={isAnswered} flagged={isMarked}>
                  {index + 1}
                </OpticalBubble>
              </button>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
