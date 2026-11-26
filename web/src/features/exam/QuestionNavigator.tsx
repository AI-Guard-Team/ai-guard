import type { SessionQuestion } from '../../api/sessions'
import { OpticalBubble } from '../../components/OpticalBubble'

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
  const answered = questions.filter((question) => selections.get(question.id) != null).length

  return (
    <nav className="answer-sheet" aria-label="Soru gezinmesi">
      <p className="kicker numeric">
        Cevap kâğıdı · {answered}/{questions.length} cevaplandı
      </p>
      <ol className="answer-sheet-grid">
        {questions.map((question, index) => {
          const isAnswered = selections.get(question.id) != null
          const isMarked = marked.has(question.id)
          const state = [isAnswered ? 'cevaplandı' : 'boş', isMarked ? 'daha sonra dönülecek' : null].filter(Boolean).join(', ')
          return (
            <li key={question.id}>
              <button
                type="button"
                className="answer-sheet-item"
                aria-current={index === current ? 'true' : undefined}
                aria-label={`${index + 1}. soru, ${state}`}
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
