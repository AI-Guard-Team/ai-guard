import { ChevronRight, Clock3, ShieldCheck } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router'
import { getSession, type SessionView } from '../../api/sessions'
import { useAuth } from '../../auth/useAuth'
import { InfoTip } from '../../components/InfoTip'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import { Stepper } from '../../components/Stepper'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AgentStep } from './AgentStep'
import { CancelledNotice } from './CancelledNotice'
import { ConsentStep } from './ConsentStep'
import { IdentityStep } from './IdentityStep'
import { ReadyStep } from './ReadyStep'
// Başlık PageHeader'ın sınıflarını kullanıyor; doğrudan açılan/yenilenen sayfada stil yoksa breadcrumb düz liste görünüyordu.
import '../../components/PageHeader.css'
import './preflight.css'

/**
 * Adımlar sunucudaki durum sırasıyla aynı (K-24). "Hazırlık" yalnızca istemcide: aday kuralları
 * süre başlamadan okusun diye kimlik doğrulamadan önce. Süre ve hak kimlik doğrulanınca başlar.
 */
const messages = defineMessages({
  tr: {
    steps: ['Aydınlatma ve rıza', 'Güvenlik ajanı', 'Hazırlık', 'Kimlik doğrulama'],
    failed: 'Oturum bilgisi yüklenemedi.',
    location: 'Konumunuz',
    myExams: 'Sınavlarım',
    current: 'Sınav öncesi adımlar',
    why: 'Neden bu adımlar?',
    whyItems: [
      'Rıza: hangi verinin neden işlendiğini okuyup onaylarsınız.',
      'Güvenlik ajanı: cihazınızda yasaklı uygulama ya da sanal makine olup olmadığını denetler.',
      'Hazırlık: kuralları süre başlamadan okursunuz.',
      'Kimlik doğrulama: kayıt fotoğrafınızla karşılaştırılır; süre ancak bundan sonra başlar.',
    ],
    description: 'Dört kısa adımı tamamlayın; süreniz kimliğiniz doğrulanınca başlar.',
    openFailed: 'Oturum açılamadı',
    pass: 'Sınava giriş kartı',
    candidate: 'Aday',
    duration: 'Süre',
    passNote: 'Süre, kimlik doğrulaması bitince başlar.',
  },
  en: {
    steps: ['Notice and consent', 'Security agent', 'Preparation', 'Identity check'],
    failed: 'The session information could not be loaded.',
    location: 'You are here',
    myExams: 'My exams',
    current: 'Pre-exam steps',
    why: 'Why these steps?',
    whyItems: [
      'Consent: you read and accept which data is processed and why.',
      'Security agent: checks your device for forbidden apps or a virtual machine.',
      'Preparation: you read the rules before the timer starts.',
      'Identity check: you are compared with your reference photo; only then does the timer start.',
    ],
    description: 'Complete four short steps; your time starts once your identity is verified.',
    openFailed: 'The session could not be opened',
    pass: 'Exam entry pass',
    candidate: 'Candidate',
    duration: 'Duration',
    passNote: 'The timer starts after identity verification.',
  },
})

type Step = 0 | 1 | 2 | 3

const RUNNING = new Set(['InProgress', 'PermissionPending', 'Disconnected'])

function stepOf(state: string): Step | null {
  if (state === 'ConsentPending') return 0
  if (state === 'AgentPairing') return 1
  if (state === 'IdentityVerification') return 2
  return null
}

/** Sınav öncesi sihirbazı (Stitch ekran 9–13). */
export function PreflightPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { sessionId = '' } = useParams()
  const load = useCallback(() => getSession(sessionId), [sessionId])
  const session = useResource(load, m.failed)
  const [step, setStep] = useState<Step | null>(null)
  const [cancelled, setCancelled] = useState(false)
  const { user } = useAuth()

  // Oturum ilk geldiğinde sihirbaz kaldığı adımdan açılır (sayfa yenilense de).
  const [seen, setSeen] = useState<SessionView | undefined>()
  if (session.data && session.data !== seen) {
    setSeen(session.data)
    if (step === null) setStep(stepOf(session.data.state))
  }

  const toAgent = useCallback(() => setStep(1), [])
  const toReady = useCallback(() => setStep(2), [])
  const toIdentity = useCallback(() => setStep(3), [])

  const view = session.data
  if (view && RUNNING.has(view.state)) return <Navigate to={`/exams/${sessionId}`} replace />
  if (view && (view.state === 'Completed' || view.state === 'Decided')) return <Navigate to="/exams" replace />

  const isCancelled = cancelled || view?.state === 'Cancelled'

  return (
    <div className="preflight">
      <header className="preflight-header">
        <nav aria-label={m.location} className="breadcrumbs">
          <ol>
            <li>
              <Link to="/exams">{m.myExams}</Link>
            </li>
            <li aria-hidden="true" className="breadcrumbs-sep">
              <ChevronRight size={14} />
            </li>
            <li aria-current="page" className="breadcrumbs-current">
              {m.current}
            </li>
          </ol>
        </nav>
        <div className="page-header-title">
          {view ? <h1>{view.examName}</h1> : <Skeleton height="2.25rem" width="min(28rem, 80%)" />}
          <InfoTip title={m.why}>
            <ul>
              {m.whyItems.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </InfoTip>
        </div>
        <p className="page-header-description">{m.description}</p>
      </header>

      {session.error && !view ? (
        <Alert tone="danger" title={m.openFailed}>
          <p>{session.error}</p>
          <Button variant="secondary" onClick={session.reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : isCancelled ? (
        <CancelledNotice />
      ) : (
        <div className="preflight-layout">
          <div className="preflight-body" aria-busy={step === null}>
            {step === null ? (
              <Skeleton height="28rem" radius="lg" />
            ) : step === 0 ? (
              <ConsentStep sessionId={sessionId} onAccepted={toAgent} onRefused={() => setCancelled(true)} />
            ) : step === 1 ? (
              <AgentStep sessionId={sessionId} onAdvance={toReady} />
            ) : step === 2 ? (
              <ReadyStep session={view!} onContinue={toIdentity} />
            ) : (
              <IdentityStep sessionId={sessionId} />
            )}
          </div>

          <aside className="entry-pass" aria-label={m.pass}>
            <div className="entry-pass-head">
              <span className="entry-pass-icon" aria-hidden="true">
                <ShieldCheck size={20} />
              </span>
              <span>
                <span className="entry-pass-label">{m.pass}</span>
                <strong className="entry-pass-exam">{view?.examName ?? '…'}</strong>
              </span>
            </div>
            <dl className="entry-pass-facts">
              <div>
                <dt>{m.candidate}</dt>
                <dd>{user?.name ?? user?.email ?? '—'}</dd>
              </div>
              <div>
                <dt>{m.duration}</dt>
                <dd>
                  <Clock3 size={14} aria-hidden="true" /> {view ? c.minutes(view.durationMinutes) : '—'}
                </dd>
              </div>
            </dl>
            <hr className="entry-pass-tear" />
            <Stepper steps={m.steps} current={step ?? 0} label={m.current} />
            <p className="entry-pass-note">{m.passNote}</p>
          </aside>
        </div>
      )}
    </div>
  )
}
