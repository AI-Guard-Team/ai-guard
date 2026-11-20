import { useRef, useState, type FormEvent } from 'react'
import { useParams } from 'react-router'
import { PairingError, canContinueWithoutAgent, pairAgent } from '../api/agentPairing'
import { ApiError } from '../api/client'
import { continueWithoutAgent, submitSessionConsent, verifyIdentity } from '../api/sessions'
import { Button } from '../components/Button'
import { FormAlert } from '../components/AuthCard'
import { Modal } from '../components/Modal'
import { captureFrames, useCamera } from './useCamera'

const STEPS = ['Aydınlatma ve rıza', 'Ajan eşleştirme', 'Kimlik doğrulama'] as const

const DISCLOSURE =
  'Sınav süresince kameranız ve güvenlik ajanı aracılığıyla sınav kuralları denetlenir. Bu veriler kimliğinizin doğrulanması ve oturumun izlenmesi için kullanılır. Rıza vermezseniz oturum iptal edilir ve sınav hakkınız kullanılmaz.'

function messageOf(error: unknown, fallback: string): string {
  if (error instanceof PairingError || error instanceof ApiError) return error.message
  if (error instanceof Error && error.message) return error.message
  return fallback
}

export function ExamStartPage() {
  const { sessionId } = useParams()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const [step, setStep] = useState(0)
  const [consent, setConsent] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [cancelled, setCancelled] = useState(false)
  const [pairingFailure, setPairingFailure] = useState<PairingError | null>(null)
  const [justification, setJustification] = useState('')
  const [verified, setVerified] = useState(false)
  const [supportOpen, setSupportOpen] = useState(false)

  if (!sessionId) {
    return (
      <section className="card">
        <h1>Sınav öncesi</h1>
        <p>Oturum bulunamadı.</p>
      </section>
    )
  }

  const grantConsent = async (granted: boolean) => {
    if (granted && !consent) {
      setError('Devam etmek için aydınlatma metnini onaylayın.')
      return
    }
    setPending(true)
    setError(null)
    try {
      await submitSessionConsent(sessionId, granted)
      if (!granted) {
        setCancelled(true)
        return
      }
      setStep(1)
    } catch (caught) {
      setError(messageOf(caught, 'Rıza kaydedilemedi. Lütfen tekrar deneyin.'))
    } finally {
      setPending(false)
    }
  }

  const pair = async () => {
    setPending(true)
    setError(null)
    setPairingFailure(null)
    try {
      await pairAgent(sessionId)
      setStep(2)
    } catch (caught) {
      if (caught instanceof PairingError) setPairingFailure(caught)
      setError(messageOf(caught, 'Ajan eşleştirilemedi. Lütfen tekrar deneyin.'))
    } finally {
      setPending(false)
    }
  }

  const skipAgent = async (event: FormEvent) => {
    event.preventDefault()
    const text = justification.trim()
    if (!text) {
      setError('Devam etmek için gerekçe yazın.')
      return
    }
    setPending(true)
    setError(null)
    try {
      await continueWithoutAgent(sessionId, text)
      setStep(2)
    } catch (caught) {
      setError(messageOf(caught, 'Ajansız devam kaydedilemedi. Lütfen tekrar deneyin.'))
    } finally {
      setPending(false)
    }
  }

  const verify = async () => {
    const video = videoRef.current
    if (!camera.live || !video) {
      setError('Önce kamerayı açın.')
      return
    }
    setPending(true)
    setError(null)
    try {
      const frames = await captureFrames(video)
      const result = await verifyIdentity(sessionId, frames)
      if (result?.passed === false) {
        setError('Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.')
        return
      }
      camera.stop()
      setVerified(true)
    } catch (caught) {
      setError(messageOf(caught, 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.'))
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="card">
      <h1>Sınav öncesi</h1>
      <p className="field-hint">Adım {Math.min(step + 1, STEPS.length)} / {STEPS.length}</p>
      <ol className="wizard-steps" aria-label="Sınav öncesi adımlar">
        {STEPS.map((label, index) => (
          <li key={label} aria-current={index === step && !verified ? 'step' : undefined} className={index < step || verified ? 'is-done' : undefined}>
            {label}
          </li>
        ))}
      </ol>
      <progress value={verified ? STEPS.length : step} max={STEPS.length} />

      <FormAlert message={error} />

      {cancelled ? (
        <p>Rıza verilmediği için oturum iptal edildi. Sınav hakkınız kullanılmadı. Alternatif yöntem için gözetmene başvurun.</p>
      ) : null}

      {!cancelled && step === 0 ? (
        <div>
          <p>{DISCLOSURE}</p>
          <label className="check">
            <input type="checkbox" checked={consent} onChange={(event) => setConsent(event.target.checked)} />
            <span>Aydınlatma metnini okudum ve sınav oturumu için açık rıza veriyorum.</span>
          </label>
          <div className="action-row">
            <Button disabled={pending} onClick={() => void grantConsent(true)}>
              {pending ? 'Kaydediliyor…' : 'Devam'}
            </Button>
            <Button variant="secondary" disabled={pending} onClick={() => void grantConsent(false)}>
              Rızayı reddet
            </Button>
          </div>
        </div>
      ) : null}

      {!cancelled && step === 1 ? (
        <div>
          <p>Güvenlik ajanı bu bilgisayarda çalışıyor olmalı. Eşleştirme kodu ekranda gösterilmez.</p>
          <div className="action-row">
            <Button disabled={pending} onClick={() => void pair()}>
              {pending ? 'Eşleştiriliyor…' : 'Ajanla eşleştir'}
            </Button>
          </div>
          {pairingFailure && canContinueWithoutAgent(pairingFailure.kind) ? (
            <form className="narrow-form" onSubmit={skipAgent} noValidate>
              <h2>Ajansız devam</h2>
              <p className="auth-lead">Ajan bulunamazsa gerekçe yazarak devam edebilirsiniz. Bu durum oturuma işlenir.</p>
              <div className="field">
                <label htmlFor="justification">Gerekçe</label>
                <textarea id="justification" value={justification} onChange={(event) => setJustification(event.target.value)} rows={3} />
              </div>
              <Button type="submit" variant="secondary" disabled={pending}>
                Ajansız devam et
              </Button>
            </form>
          ) : null}
        </div>
      ) : null}

      {!cancelled && step === 2 && !verified ? (
        <div>
          <p>Yüzünüz çerçevenin ortasında ve iyi aydınlatılmış olsun. Birkaç kare alınacak. Kamera yalnızca rıza verildikten sonra açılır.</p>
          <video ref={videoRef} className={camera.live ? 'camera-preview' : 'visually-hidden'} muted playsInline />
          {camera.error ? <FormAlert message={camera.error} /> : null}
          <div className="action-row">
            <Button variant="secondary" disabled={pending} onClick={() => void camera.start()}>
              Kamerayı aç
            </Button>
            <Button disabled={pending || !camera.live} onClick={() => void verify()}>
              {pending ? 'Doğrulanıyor…' : 'Doğrulamayı başlat'}
            </Button>
            {error ? (
              <Button variant="secondary" onClick={() => setSupportOpen(true)}>
                Destek talep et
              </Button>
            ) : null}
          </div>
        </div>
      ) : null}

      {verified ? <p>Kimlik doğrulandı. Sınav öncesi adımlar tamamlandı.</p> : null}

      <Modal open={supportOpen} title="Destek talep et" onClose={() => setSupportOpen(false)}>
        <p>Doğrulama tamamlanamadı. Gözetmene bu oturum numarasını iletin:</p>
        <p>
          <strong>{sessionId}</strong>
        </p>
      </Modal>
    </section>
  )
}
