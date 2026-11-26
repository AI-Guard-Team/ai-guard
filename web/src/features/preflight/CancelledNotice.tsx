import { ArrowLeft, Info } from 'lucide-react'
import { Link } from 'react-router'

/** Rıza reddedildi: oturum iptal, hak tüketilmedi (FR-10). Alternatif yöntem ekranı (Stitch 17) sonraki PR'da. */
export function CancelledNotice() {
  return (
    <section className="step-panel" aria-labelledby="cancelled-title">
      <header>
        <h2 id="cancelled-title">Oturum iptal edildi</h2>
        <p>Sınav izleme için rıza vermediğiniz için bu oturum iptal edildi.</p>
      </header>
      <ul className="fact-list">
        <li>
          <Info size={18} aria-hidden="true" />
          <span>
            <strong>Sınav hakkınız kullanılmadı.</strong>
            <span>Fikrinizi değiştirirseniz sınav penceresi açıkken aynı sınavı yeniden başlatabilirsiniz.</span>
          </span>
        </li>
        <li>
          <Info size={18} aria-hidden="true" />
          <span>
            <strong>Alternatif yöntem</strong>
            <span>Gözetimsiz bir sınav yöntemi için kurumunuzun sınav sorumlusuna başvurun.</span>
          </span>
        </li>
      </ul>
      <div className="step-actions step-actions-start">
        <Link className="button button-secondary button-md" to="/exams">
          <ArrowLeft size={18} aria-hidden="true" />
          Sınavlarıma dön
        </Link>
      </div>
    </section>
  )
}
