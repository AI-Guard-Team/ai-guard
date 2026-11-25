import type { SessionQuestion } from '../../api/sessions'

interface QuestionNavigatorProps {
  questions: SessionQuestion[]
  current: number
  selections: Map<string, number | null>
  marked: ReadonlySet<string>
  onSelect: (index: number) => void
}

/** Soru gezintisi ve tamamlanma oranı. Durum renk ve ekran okuyucu metniyle birlikte. */
export function QuestionNavigator({ questions, current, selections, marked, onSelect }: QuestionNavigatorProps) {
  const answered = questions.filter((question) => selections.get(question.id) != null).length
  const percent = questions.length === 0 ? 0 : Math.round((answered / questions.length) * 100)

  return (
    <nav className="question-nav" aria-label="Soru gezinmesi">
      <div className="question-nav-summary">
        <p className="numeric">
          <strong>{answered}</strong> / {questions.length} cevaplandı
        </p>
        <div className="question-nav-progress" aria-hidden="true">
          <span style={{ width: `${percent}%` }} />
        </div>
        <ul className="question-nav-legend" aria-hidden="true">
          <li className="is-answered">Cevaplandı</li>
          <li className="is-marked">İşaretli</li>
          <li>Boş</li>
        </ul>
      </div>
      <ol className="question-nav-grid">
        {questions.map((question, index) => {
          const isAnswered = selections.get(question.id) != null
          const isMarked = marked.has(question.id)
          const state = [isAnswered ? 'cevaplandı' : 'boş', isMarked ? 'daha sonra dönülecek' : null].filter(Boolean).join(', ')
          return (
            <li key={question.id}>
              <button
                type="button"
                className={[isAnswered && 'is-answered', isMarked && 'is-marked'].filter(Boolean).join(' ') || undefined}
                aria-current={index === current ? 'true' : undefined}
                aria-label={`${index + 1}. soru, ${state}`}
                onClick={() => onSelect(index)}
              >
                {index + 1}
              </button>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
