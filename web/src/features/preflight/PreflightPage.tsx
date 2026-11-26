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
import { AgentStep } from './AgentStep'
import { CancelledNotice } from './CancelledNotice'
import { ConsentStep } from './ConsentStep'
import { IdentityStep } from './IdentityStep'
import { ReadyStep } from './ReadyStep'
import './preflight.css'

/**
 * Adımlar sunucudaki durum sırasıyla aynı (K-24). "Hazırlık" yalnızca istemcide: aday kuralları
 * süre başlamadan okusun diye kimlik doğrulamadan önce. Süre ve hak kimlik doğrulanınca başlar.
 */
const PREFLIGHT_STEPS = ['Aydınlatma ve rıza', 'Güvenlik ajanı', 'Hazırlık', 'Kimlik doğrulama'] as const

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
  const { sessionId = '' } = useParams()
  const load = useCallback(() => getSession(sessionId), [sessionId])
  const session = useResource(load, 'Oturum bilgisi yüklenemedi.')
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
        <nav aria-label="Konumunuz" className="breadcrumbs">
          <ol>
            <li>
              <Link to="/exams">Sınavlarım</Link>
            </li>
            <li aria-hidden="true" className="breadcrumbs-sep">
              <ChevronRight size={14} />
            </li>
            <li aria-current="page" className="breadcrumbs-current">
              Sınav öncesi adımlar
            </li>
          </ol>
        </nav>
        <div className="page-header-title">
          {view ? <h1>{view.examName}</h1> : <Skeleton height="2.25rem" width="min(28rem, 80%)" />}
          <InfoTip title="Neden bu adımlar?">
            <ul>
              <li>Rıza: hangi verinin neden işlendiğini okuyup onaylarsınız.</li>
              <li>Güvenlik ajanı: cihazınızda yasaklı uygulama ya da sanal makine olup olmadığını denetler.</li>
              <li>Hazırlık: kuralları süre başlamadan okursunuz.</li>
              <li>Kimlik doğrulama: kayıt fotoğrafınızla karşılaştırılır; süre ancak bundan sonra başlar.</li>
            </ul>
          </InfoTip>
        </div>
        <p className="page-header-description">Dört kısa adımı tamamlayın; süreniz kimliğiniz doğrulanınca başlar.</p>
      </header>

      {session.error && !view ? (
        <Alert tone="danger" title="Oturum açılamadı">
          <p>{session.error}</p>
          <Button variant="secondary" onClick={session.reload}>
            Yeniden dene
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

          <aside className="entry-pass" aria-label="Sınava giriş kartı">
            <div className="entry-pass-head">
              <span className="entry-pass-icon" aria-hidden="true">
                <ShieldCheck size={20} />
              </span>
              <span>
                <span className="entry-pass-label">Sınava giriş kartı</span>
                <strong className="entry-pass-exam">{view?.examName ?? '…'}</strong>
              </span>
            </div>
            <dl className="entry-pass-facts">
              <div>
                <dt>Aday</dt>
                <dd>{user?.name ?? user?.email ?? '—'}</dd>
              </div>
              <div>
                <dt>Süre</dt>
                <dd>
                  <Clock3 size={14} aria-hidden="true" /> {view ? `${view.durationMinutes} dakika` : '—'}
                </dd>
              </div>
            </dl>
            <hr className="entry-pass-tear" />
            <Stepper steps={PREFLIGHT_STEPS} current={step ?? 0} label="Sınav öncesi adımlar" />
            <p className="entry-pass-note">Süre, kimlik doğrulaması bitince başlar.</p>
          </aside>
        </div>
      )}
    </div>
  )
}
