import { ArrowLeft, Info } from 'lucide-react'
import { Link } from 'react-router'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    title: 'Oturum iptal edildi',
    text: 'Sınav izleme için rıza vermediğiniz için bu oturum iptal edildi.',
    unused: 'Sınav hakkınız kullanılmadı.',
    unusedText: 'Fikrinizi değiştirirseniz sınav penceresi açıkken aynı sınavı yeniden başlatabilirsiniz.',
    alternative: 'Alternatif yöntem',
    alternativeText: 'Gözetimsiz bir sınav yöntemi için kurumunuzun sınav sorumlusuna başvurun.',
    back: 'Sınavlarıma dön',
  },
  en: {
    title: 'Session cancelled',
    text: 'This session was cancelled because you did not consent to exam monitoring.',
    unused: 'Your attempt was not used.',
    unusedText: 'If you change your mind, you can start the same exam again while its window is open.',
    alternative: 'Alternative method',
    alternativeText: 'Contact your institution’s exam coordinator for an unmonitored exam method.',
    back: 'Back to my exams',
  },
})

/** Rıza reddedildi: oturum iptal, hak tüketilmedi (FR-10). Alternatif yöntem ekranı (Stitch 17) sonraki PR'da. */
export function CancelledNotice() {
  const m = useMessages(messages)
  return (
    <section className="step-panel" aria-labelledby="cancelled-title">
      <header>
        <h2 id="cancelled-title">{m.title}</h2>
        <p>{m.text}</p>
      </header>
      <ul className="fact-list">
        <li>
          <Info size={18} aria-hidden="true" />
          <span>
            <strong>{m.unused}</strong>
            <span>{m.unusedText}</span>
          </span>
        </li>
        <li>
          <Info size={18} aria-hidden="true" />
          <span>
            <strong>{m.alternative}</strong>
            <span>{m.alternativeText}</span>
          </span>
        </li>
      </ul>
      <div className="step-actions step-actions-start">
        <Link className="button button-secondary button-md" to="/exams">
          <ArrowLeft size={18} aria-hidden="true" />
          {m.back}
        </Link>
      </div>
    </section>
  )
}
