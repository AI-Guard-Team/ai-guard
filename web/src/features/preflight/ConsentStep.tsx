import { ArrowRight, FileCheck2, ShieldCheck, UserCheck } from 'lucide-react'
import { useState } from 'react'
import { ApiError } from '../../api/client'
import { submitSessionConsent } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Checkbox } from '../../components/Checkbox'
import { Modal } from '../../components/Modal'
import { ConsentTextPanel } from '../consent/ConsentTextPanel'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { useConsentText } from '../consent/useConsentText'

const FACT_ICONS = [ShieldCheck, FileCheck2, UserCheck] as const

const messages = defineMessages({
  tr: {
    // Platformun gerçek davranışı (K-26, K-30, K-31); Stitch ekran 9'daki üç ilke.
    facts: [
      { title: 'Sürekli video ya da ses kaydı tutulmaz', text: 'Kamera ve mikrofon sınav boyunca anlık analiz edilir, kayıt dosyası oluşmaz.' },
      { title: 'Yalnızca şüpheli anların kareleri saklanır', text: 'Bu kareler şifreli tutulur ve saklama süresi dolunca silinir.' },
      { title: 'Kararı bir değerlendirici verir', text: 'Sistem kopya kararı vermez; işaretlenen oturumu yetkili kişi inceler.' },
    ],
    agreeFirst: 'Devam etmek için aydınlatma metnini onaylayın.',
    failed: 'Rıza kaydedilemedi. Lütfen tekrar deneyin.',
    title: 'Sınav öncesi bilgilendirme ve açık rıza',
    text: 'Sınav sırasında hangi verilerin neden işlendiğini okuyun. Rıza vermezseniz oturum iptal edilir, sınav hakkınız kullanılmaz.',
    label: 'Aydınlatma metnini okudum; sınav süresince kamera, mikrofon, tarayıcı ve cihaz verilerimin bu metinde yazan amaçlarla işlenmesine açık rıza veriyorum.',
    refuse: 'Rıza vermiyorum',
    accept: 'Kabul ediyorum, devam',
    confirmTitle: 'Rıza vermeden çıkılsın mı?',
    confirmText: 'Bu oturum iptal edilir. Sınav hakkınız kullanılmaz; pencere açıkken sınavı yeniden başlatabilirsiniz.',
    cancelSession: 'Oturumu iptal et',
  },
  en: {
    facts: [
      { title: 'No continuous video or audio recording', text: 'The camera and microphone are analysed in real time during the exam; no recording file is created.' },
      { title: 'Only frames of suspicious moments are kept', text: 'These frames are stored encrypted and deleted when the retention period ends.' },
      { title: 'A reviewer makes the decision', text: 'The system does not decide on cheating; a qualified person reviews any flagged session.' },
    ],
    agreeFirst: 'Accept the privacy notice to continue.',
    failed: 'Your consent could not be saved. Please try again.',
    title: 'Pre-exam notice and explicit consent',
    text: 'Read which data is processed during the exam and why. If you do not consent, the session is cancelled and your attempt is not used.',
    label: 'I have read the privacy notice and give explicit consent to processing my camera, microphone, browser and device data during the exam for the purposes described in it.',
    refuse: 'I do not consent',
    accept: 'I agree, continue',
    confirmTitle: 'Leave without consenting?',
    confirmText: 'This session will be cancelled. Your attempt is not used; you can start the exam again while the window is open.',
    cancelSession: 'Cancel session',
  },
})

/** Sunucunun reddi rıza metninin eskimesinden mi; dilden bağımsız, ham gövdeden okunur. */
function staleConsentText(error: ApiError): boolean {
  const title = (error.body as { title?: unknown } | undefined)?.title
  return error.status === 400 && typeof title === 'string' && (title.includes('Rıza metni') || title.includes('consent text is out of date'))
}

interface ConsentStepProps {
  sessionId: string
  onAccepted: () => void
  onRefused: () => void
}

/** Aydınlatma ve açık rıza (Stitch ekran 9; `POST /api/sessions/{id}/consent`, A3). */
export function ConsentStep({ sessionId, onAccepted, onRefused }: ConsentStepProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const text = useConsentText('Session')
  const [agreed, setAgreed] = useState(false)
  const [pending, setPending] = useState<'accept' | 'refuse' | null>(null)
  const [error, setError] = useState<{ title?: string; text: string } | null>(null)
  const [confirmRefuse, setConfirmRefuse] = useState(false)

  const send = async (granted: boolean) => {
    const version = text.data?.version
    if (!version) return
    if (granted && !agreed) {
      setError({ text: m.agreeFirst })
      return
    }
    setPending(granted ? 'accept' : 'refuse')
    setError(null)
    try {
      await submitSessionConsent(sessionId, granted, version)
      setConfirmRefuse(false)
      if (granted) onAccepted()
      else onRefused()
    } catch (caught) {
      setConfirmRefuse(false)
      // 409: kayıt fotoğrafı yok ya da oturum bu adımda değil; 400: metin sürümü eski. Neden "title"da.
      if (caught instanceof ApiError && staleConsentText(caught)) text.reload()
      setError(caught instanceof ApiError ? { title: caught.title, text: caught.message } : { text: m.failed })
    } finally {
      setPending(null)
    }
  }

  return (
    <section className="step-panel" aria-labelledby="consent-step-title">
      <header>
        <h2 id="consent-step-title">{m.title}</h2>
        <p>{m.text}</p>
      </header>

      <ul className="fact-list">
        {m.facts.map(({ title, text: body }, index) => {
          const Icon = FACT_ICONS[index]
          return (
          <li key={title}>
            <Icon size={18} aria-hidden="true" />
            <span>
              <strong>{title}</strong>
              <span>{body}</span>
            </span>
          </li>
          )
        })}
      </ul>

      <div>
        <ConsentTextPanel resource={text} />
        <Checkbox
          checked={agreed}
          disabled={!text.data}
          onChange={(event) => {
            setAgreed(event.target.checked)
            setError(null)
          }}
          label={m.label}
        />
      </div>

      {error ? (
        <Alert tone="danger" title={error.title !== error.text ? error.title : undefined}>
          {error.text}
        </Alert>
      ) : null}

      <div className="step-actions">
        <Button variant="secondary" disabled={!text.data || pending !== null} onClick={() => setConfirmRefuse(true)}>
          {m.refuse}
        </Button>
        <Button
          loading={pending === 'accept'}
          disabled={!text.data || !agreed || pending !== null}
          onClick={() => void send(true)}
          iconEnd={pending === 'accept' ? undefined : <ArrowRight size={18} aria-hidden="true" />}
        >
          {pending === 'accept' ? c.saving : m.accept}
        </Button>
      </div>

      <Modal open={confirmRefuse} title={m.confirmTitle} onClose={() => setConfirmRefuse(false)}>
        <p>{m.confirmText}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmRefuse(false)}>
            {c.cancel}
          </Button>
          <Button variant="danger" loading={pending === 'refuse'} onClick={() => void send(false)}>
            {m.cancelSession}
          </Button>
        </div>
      </Modal>
    </section>
  )
}
