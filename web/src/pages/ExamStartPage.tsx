import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { AGENT_DOWNLOAD_URL, PairingError, pairAgent } from '../api/agentPairing'
import { ApiError } from '../api/client'
import {
  AGENT_STATUS_POLL_MS,
  continueAgent,
  getAgentStatus,
  submitSessionConsent,
  verifyIdentity,
  type AgentContinueMode,
  type AgentStatus,
} from '../api/sessions'
import { Button } from '../components/Button'
import { FormAlert } from '../components/Alert'
import { Modal } from '../components/Modal'
import { captureFrames, useCamera } from './useCamera'

const STEPS = ['Aydınlatma ve rıza', 'Ajan eşleştirme', 'Kimlik doğrulama'] as const

const DISCLOSURE =
  'Sınav süresince kameranız ve güvenlik ajanı aracılığıyla sınav kuralları denetlenir. Bu veriler kimliğinizin doğrulanması ve oturumun izlenmesi için kullanılır. Rıza vermezseniz oturum iptal edilir ve sınav hakkınız kullanılmaz.'

/**
 * Rıza ucu 409'da nedeni ProblemDetails `title` alanında Türkçe verir (kayıt fotoğrafı yok, oturum rıza adımında değil).
 * İstemcinin genel 409 metni bu nedeni gizlediği için burada başlık gösterilir.
 */
function consentConflictOf(error: unknown): string | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null
  const title = (error.body as { title?: unknown } | null | undefined)?.title
  return typeof title === 'string' && title ? title : null
}

function messageOf(error: unknown, fallback: string): string {
  if (error instanceof PairingError || error instanceof ApiError) return error.message
  if (error instanceof Error && error.message) return error.message
  return fallback
}

async function readAgentStatus(sessionId: string): Promise<AgentStatus | null> {
  try {
    return (await getAgentStatus(sessionId)) ?? null
  } catch {
    // Bir sonraki yoklama düzelir. Adayı her denemede uyarmayız.
    return null
  }
}

function AgentSetupGuide() {
  return (
    <div>
      <p>
        Sınava girmeden önce bu bilgisayarda AI-Guard güvenlik ajanının çalışıyor olması gerekir. Ajan yalnızca sınav
        sırasında çalışır; yönetici izni istemez ve bilgisayarınıza bir şey kurmaz. Windows 10 ya da 11, 64 bit gerekir.
      </p>
      <ol className="setup-steps">
        <li>
          {AGENT_DOWNLOAD_URL ? (
            <a href={AGENT_DOWNLOAD_URL}>Güvenlik ajanını indir</a>
          ) : (
            'Güvenlik ajanını indir'
          )}
          : <code>aiguard-agent.exe</code> (yaklaşık 45 MB). Dosyayı İndirilenler klasöründe bırakabilirsiniz.
        </li>
        <li>
          Dosyaya çift tıklayın. Siyah pencerede “AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi
          kapatmayın.” yazısını görünce bu sayfaya dönüp Ajanla eşleştir’e basın. Kod yazmanız gerekmez.
        </li>
      </ol>
      <p className="field-hint">
        Windows “bilgisayarınızı korudu” derse Ek bilgi’ye, ardından Yine de çalıştır’a basın. Tarayıcı “bu dosya yaygın
        olarak indirilmiyor” derse indirme listesinden Sakla’yı seçin. Eşleştirmede “Bu cihazdaki diğer uygulamalara ve
        hizmetlere erişin” izni çıkarsa İzin ver’i seçin.
      </p>
    </div>
  )
}

function ContinueForm({
  title,
  lead,
  pending,
  justification,
  onJustification,
  onSubmit,
  submitLabel,
}: {
  title: string
  lead: string
  pending: boolean
  justification: string
  onJustification: (value: string) => void
  onSubmit: (event: FormEvent) => void
  submitLabel: string
}) {
  return (
    <form className="narrow-form" onSubmit={onSubmit} noValidate>
      <h2>{title}</h2>
      <p className="auth-lead">{lead}</p>
      <div className="field">
        <label htmlFor="justification">Gerekçe</label>
        <textarea
          id="justification"
          value={justification}
          onChange={(event) => onJustification(event.target.value)}
          rows={3}
          maxLength={2000}
        />
      </div>
      <Button type="submit" variant="secondary" disabled={pending}>
        {submitLabel}
      </Button>
    </form>
  )
}

function AgentPairingStep({ sessionId, onAdvance }: { sessionId: string; onAdvance: () => void }) {
  const request = useRef(0)
  const mounted = useRef(true)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [justification, setJustification] = useState('')
  const [status, setStatus] = useState<AgentStatus | null>(null)

  const pull = useCallback(async () => {
    const id = ++request.current
    const next = await readAgentStatus(sessionId)
    if (!mounted.current || id !== request.current || !next) return
    setStatus(next)
    if (next.state === 'IdentityVerification') onAdvance()
  }, [sessionId, onAdvance])

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  useEffect(() => {
    void pull()
    const timer = window.setInterval(() => void pull(), AGENT_STATUS_POLL_MS)
    return () => {
      request.current += 1
      window.clearInterval(timer)
    }
  }, [pull])

  const pair = async () => {
    setPending(true)
    setError(null)
    try {
      await pairAgent(sessionId)
      await pull()
    } catch (caught) {
      setError(messageOf(caught, 'Ajan eşleştirilemedi. Lütfen tekrar deneyin.'))
    } finally {
      if (mounted.current) setPending(false)
    }
  }

  const submitContinue = async (event: FormEvent, mode: AgentContinueMode) => {
    event.preventDefault()
    const reason = justification.trim()
    if (!reason) {
      setError('Devam etmek için gerekçe yazın.')
      return
    }
    setPending(true)
    setError(null)
    try {
      await continueAgent(sessionId, mode, reason)
      onAdvance()
    } catch (caught) {
      // Ajan çalışırken ajansız devam 409 döner. Bu cümle adaya gösterilmez; form da ancak sunucu izin verince durur.
      if (caught instanceof ApiError && caught.status === 409 && mode === 'noAgent') {
        setError('Ajansız devam edilemiyor.')
        await pull()
        return
      }
      setError(messageOf(caught, 'Devam kaydedilemedi. Lütfen tekrar deneyin.'))
    } finally {
      if (mounted.current) setPending(false)
    }
  }

  const indicators = status?.indicators ?? []
  const showNoAgent = status?.canContinueWithoutAgent === true && status.canContinueIndicated !== true

  return (
    <div>
      <AgentSetupGuide />
      <div className="action-row">
        <Button disabled={pending} onClick={() => void pair()}>
          {pending ? 'Eşleştiriliyor…' : 'Ajanla eşleştir'}
        </Button>
      </div>
      <FormAlert message={error} />
      {indicators.length > 0 ? (
        <ul className="indicator-list" aria-label="Ajan uyarıları">
          {indicators.map((indicator) => (
            <li key={`${indicator.type}:${indicator.name}`}>
              <p>{indicator.message}</p>
              <p className="field-hint">{indicator.fix}</p>
            </li>
          ))}
        </ul>
      ) : null}
      {status?.state === 'AgentPairing' && status.paired && indicators.length === 0 ? (
        <p>Eşleşme alındı. Denetim birkaç saniye içinde yenilenir.</p>
      ) : null}
      {status?.canContinueIndicated ? (
        <ContinueForm
          title="Gerekçeyle devam"
          lead="Uyarıyı gideremiyorsanız gerekçenizi yazarak devam edebilirsiniz. Bu durum oturumunuza işlenir ve sınavınız incelenir."
          pending={pending}
          justification={justification}
          onJustification={setJustification}
          onSubmit={(event) => void submitContinue(event, 'indicated')}
          submitLabel="Gerekçeyle devam et"
        />
      ) : null}
      {showNoAgent ? (
        <ContinueForm
          title="Ajansız devam"
          lead="Ajan açılmıyorsa gerekçenizi yazarak ajansız devam edebilirsiniz. Cihaz denetimi yapılmaz ve sınavınız incelemeye alınır."
          pending={pending}
          justification={justification}
          onJustification={setJustification}
          onSubmit={(event) => void submitContinue(event, 'noAgent')}
          submitLabel="Ajansız devam et"
        />
      ) : null}
    </div>
  )
}

export function ExamStartPage() {
  const { sessionId } = useParams()
  const navigate = useNavigate()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const [step, setStep] = useState(0)
  const [initializing, setInitializing] = useState(Boolean(sessionId))
  const [consent, setConsent] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [cancelled, setCancelled] = useState(false)
  const [supportOpen, setSupportOpen] = useState(false)
  const goToIdentity = useCallback(() => setStep(2), [])

  // Belirteçler bellekte tutulduğu için sayfa yenilenince aday yeniden giriş yapıp buraya döner.
  // Oturum rızayı geçmişse sihirbaz kaldığı adımdan açılır; yoksa rıza ucu 409 döner ve aday ilerleyemez.
  useEffect(() => {
    if (!sessionId) return
    let active = true
    void readAgentStatus(sessionId).then((status) => {
      if (!active) return
      if (status?.state === 'IdentityVerification') setStep(2)
      else if (status?.state === 'AgentPairing') setStep(1)
      setInitializing(false)
    })
    return () => {
      active = false
    }
  }, [sessionId])

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
      setError(consentConflictOf(caught) ?? messageOf(caught, 'Rıza kaydedilemedi. Lütfen tekrar deneyin.'))
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
      navigate(`/exams/${sessionId}`, { replace: true })
    } catch (caught) {
      setError(messageOf(caught, 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.'))
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="card">
      <h1>Sınav öncesi</h1>
      {initializing ? (
        <p>Yükleniyor…</p>
      ) : (
        <>
          <p className="field-hint">Adım {Math.min(step + 1, STEPS.length)} / {STEPS.length}</p>
          <ol className="wizard-steps" aria-label="Sınav öncesi adımlar">
            {STEPS.map((label, index) => (
              <li key={label} aria-current={index === step ? 'step' : undefined} className={index < step ? 'is-done' : undefined}>
                {label}
              </li>
            ))}
          </ol>
          <progress value={step} max={STEPS.length} />
        </>
      )}

      <FormAlert message={error} />

      {!initializing && cancelled ? (
        <p>Rıza verilmediği için oturum iptal edildi. Sınav hakkınız kullanılmadı. Alternatif yöntem için gözetmene başvurun.</p>
      ) : null}

      {!initializing && !cancelled && step === 0 ? (
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

      {!initializing && !cancelled && step === 1 ? <AgentPairingStep sessionId={sessionId} onAdvance={goToIdentity} /> : null}

      {!initializing && !cancelled && step === 2 ? (
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

      <Modal open={supportOpen} title="Destek talep et" onClose={() => setSupportOpen(false)}>
        <p>Doğrulama tamamlanamadı. Gözetmene bu oturum numarasını iletin:</p>
        <p>
          <strong>{sessionId}</strong>
        </p>
      </Modal>
    </section>
  )
}
