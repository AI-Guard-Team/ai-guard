import { ArrowLeft, Building2, Info, Mail, Phone } from 'lucide-react'
import { Link } from 'react-router'
import { usePlatformSettings } from '../../hooks/usePlatformSettings'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    title: 'Oturum iptal edildi',
    text: 'Sınav izleme için rıza vermediğiniz için bu oturum iptal edildi.',
    unused: 'Sınav hakkınız kullanılmadı.',
    unusedText: 'Fikrinizi değiştirirseniz sınav penceresi açıkken aynı sınavı yeniden başlatabilirsiniz.',
    alternative: 'Alternatif sınav yöntemi',
    alternativeFallback: 'Gözetimsiz bir sınav yöntemi için kurumunuzun sınav sorumlusuna başvurun.',
    contact: 'İletişim',
    email: (address: string) => `${address} adresine e-posta gönder`,
    call: (phone: string) => `${phone} numarasını ara`,
    back: 'Sınavlarıma dön',
  },
  en: {
    title: 'Session cancelled',
    text: 'This session was cancelled because you did not consent to exam monitoring.',
    unused: 'Your attempt was not used.',
    unusedText: 'If you change your mind, you can start the same exam again while its window is open.',
    alternative: 'Alternative exam method',
    alternativeFallback: 'Contact your institution’s exam coordinator for an unmonitored exam method.',
    contact: 'Contact',
    email: (address: string) => `Send an email to ${address}`,
    call: (phone: string) => `Call ${phone}`,
    back: 'Back to my exams',
  },
})

/**
 * Rıza reddedildi: oturum iptal, hak tüketilmedi (FR-10). Alternatif yöntem (Stitch 17): metin ve iletişim
 * yöneticinin platform ayarından (K-49); ayarlanmadıysa genel yönlendirme. Metin düz metin olarak çiziliyor.
 */
export function CancelledNotice() {
  const m = useMessages(messages)
  const settings = usePlatformSettings()
  const email = settings?.supportEmail
  const phone = settings?.supportPhone

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
      </ul>

      <section className="alternative-card" aria-labelledby="alternative-title">
        <span className="alternative-icon" aria-hidden="true">
          <Building2 size={20} />
        </span>
        <div className="alternative-body">
          <h3 id="alternative-title">{m.alternative}</h3>
          <p className="alternative-text">{settings?.alternativeMethodText || m.alternativeFallback}</p>
          {email || phone ? (
            <div className="alternative-contact" role="group" aria-label={m.contact}>
              {email ? (
                <a className="button button-secondary button-md" href={`mailto:${encodeURIComponent(email).replace('%40', '@')}`} aria-label={m.email(email)}>
                  <Mail size={16} aria-hidden="true" />
                  {email}
                </a>
              ) : null}
              {phone ? (
                <a className="button button-ghost button-md" href={`tel:${phone.replace(/[^0-9+]/g, '')}`} aria-label={m.call(phone)}>
                  <Phone size={16} aria-hidden="true" />
                  {phone}
                </a>
              ) : null}
            </div>
          ) : null}
        </div>
      </section>

      <div className="step-actions step-actions-start">
        <Link className="button button-secondary button-md" to="/exams">
          <ArrowLeft size={18} aria-hidden="true" />
          {m.back}
        </Link>
      </div>
    </section>
  )
}
