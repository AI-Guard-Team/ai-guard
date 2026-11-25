import { ArrowRight, FileCheck2, ShieldCheck, UserCheck } from 'lucide-react'
import { useState } from 'react'
import { ApiError } from '../../api/client'
import { submitSessionConsent } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Checkbox } from '../../components/Checkbox'
import { Modal } from '../../components/Modal'
import { ConsentTextPanel } from '../consent/ConsentTextPanel'
import { useConsentText } from '../consent/useConsentText'

// Platformun gerçek davranışı (K-26, K-30, K-31); Stitch ekran 9'daki üç ilke.
const FACTS = [
  { icon: ShieldCheck, title: 'Sürekli video ya da ses kaydı tutulmaz', text: 'Kamera ve mikrofon sınav boyunca anlık analiz edilir, kayıt dosyası oluşmaz.' },
  { icon: FileCheck2, title: 'Yalnızca şüpheli anların kareleri saklanır', text: 'Bu kareler şifreli tutulur ve saklama süresi dolunca silinir.' },
  { icon: UserCheck, title: 'Kararı bir değerlendirici verir', text: 'Sistem kopya kararı vermez; işaretlenen oturumu yetkili kişi inceler.' },
] as const

interface ConsentStepProps {
  sessionId: string
  onAccepted: () => void
  onRefused: () => void
}

/** Aydınlatma ve açık rıza (Stitch ekran 9; `POST /api/sessions/{id}/consent`, A3). */
export function ConsentStep({ sessionId, onAccepted, onRefused }: ConsentStepProps) {
  const text = useConsentText('Session')
  const [agreed, setAgreed] = useState(false)
  const [pending, setPending] = useState<'accept' | 'refuse' | null>(null)
  const [error, setError] = useState<{ title?: string; text: string } | null>(null)
  const [confirmRefuse, setConfirmRefuse] = useState(false)

  const send = async (granted: boolean) => {
    const version = text.data?.version
    if (!version) return
    if (granted && !agreed) {
      setError({ text: 'Devam etmek için aydınlatma metnini onaylayın.' })
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
      if (caught instanceof ApiError && caught.status === 400 && caught.title?.includes('Rıza metni')) text.reload()
      setError(caught instanceof ApiError ? { title: caught.title, text: caught.message } : { text: 'Rıza kaydedilemedi. Lütfen tekrar deneyin.' })
    } finally {
      setPending(null)
    }
  }

  return (
    <section className="step-panel" aria-labelledby="consent-step-title">
      <header>
        <h2 id="consent-step-title">Sınav öncesi bilgilendirme ve açık rıza</h2>
        <p>Sınav sırasında hangi verilerin neden işlendiğini okuyun. Rıza vermezseniz oturum iptal edilir, sınav hakkınız kullanılmaz.</p>
      </header>

      <ul className="fact-list">
        {FACTS.map(({ icon: Icon, title, text: body }) => (
          <li key={title}>
            <Icon size={18} aria-hidden="true" />
            <span>
              <strong>{title}</strong>
              <span>{body}</span>
            </span>
          </li>
        ))}
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
          label="Aydınlatma metnini okudum; sınav süresince kamera, mikrofon, tarayıcı ve cihaz verilerimin bu metinde yazan amaçlarla işlenmesine açık rıza veriyorum."
        />
      </div>

      {error ? (
        <Alert tone="danger" title={error.title !== error.text ? error.title : undefined}>
          {error.text}
        </Alert>
      ) : null}

      <div className="step-actions">
        <Button variant="secondary" disabled={!text.data || pending !== null} onClick={() => setConfirmRefuse(true)}>
          Rıza vermiyorum
        </Button>
        <Button
          loading={pending === 'accept'}
          disabled={!text.data || !agreed || pending !== null}
          onClick={() => void send(true)}
          iconEnd={pending === 'accept' ? undefined : <ArrowRight size={18} aria-hidden="true" />}
        >
          {pending === 'accept' ? 'Kaydediliyor…' : 'Kabul ediyorum, devam'}
        </Button>
      </div>

      <Modal open={confirmRefuse} title="Rıza vermeden çıkılsın mı?" onClose={() => setConfirmRefuse(false)}>
        <p>Bu oturum iptal edilir. Sınav hakkınız kullanılmaz; pencere açıkken sınavı yeniden başlatabilirsiniz.</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmRefuse(false)}>
            Vazgeç
          </Button>
          <Button variant="danger" loading={pending === 'refuse'} onClick={() => void send(false)}>
            Oturumu iptal et
          </Button>
        </div>
      </Modal>
    </section>
  )
}
