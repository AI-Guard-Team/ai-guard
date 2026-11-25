import { CircleCheck, Link2, LoaderCircle, RefreshCw, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { PairingError, pairAgent } from '../../api/agentPairing'
import { ApiError } from '../../api/client'
import { continueAgent, type AgentContinueMode, type AgentStatus } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { AgentSetupGuide } from './AgentSetupGuide'
import { JustificationForm } from './JustificationForm'
import { useAgentStatus } from './useAgentStatus'
import './AgentStep.css'

function messageOf(error: unknown, fallback: string): string {
  if (error instanceof PairingError || error instanceof ApiError) return error.message
  return fallback
}

type Phase = 'waiting' | 'checking' | 'indicated'

function phaseOf(status: AgentStatus | null): Phase {
  if (status?.indicators.length) return 'indicated'
  if (status?.paired) return 'checking'
  return 'waiting'
}

/** Güvenlik ajanı eşleştirme ve göstergeler (Stitch ekran 10–11; Y1, K-19, K-22, K-25). */
export function AgentStep({ sessionId, onAdvance }: { sessionId: string; onAdvance: () => void }) {
  const { status, refresh } = useAgentStatus(sessionId, onAdvance)
  const [pending, setPending] = useState<'pair' | 'continue' | 'refresh' | null>(null)
  const [error, setError] = useState<string | null>(null)

  const pair = async () => {
    setPending('pair')
    setError(null)
    try {
      // Kod ekrana yazılmaz; doğrudan 127.0.0.1'deki ajana gider (K-19).
      await pairAgent(sessionId)
      await refresh()
    } catch (caught) {
      setError(messageOf(caught, 'Ajan eşleştirilemedi. Lütfen tekrar deneyin.'))
    } finally {
      setPending(null)
    }
  }

  const recheck = async () => {
    setPending('refresh')
    await refresh()
    setPending(null)
  }

  const proceed = async (mode: AgentContinueMode, reason: string) => {
    setPending('continue')
    setError(null)
    try {
      await continueAgent(sessionId, mode, reason)
      onAdvance()
    } catch (caught) {
      // Ajan çalışırken ajansız devam 409 döner. Ajanın çalıştığı adaya söylenmez; form sunucu izin verince kalkar.
      if (caught instanceof ApiError && caught.status === 409 && mode === 'noAgent') {
        setError('Ajansız devam edilemiyor.')
        await refresh()
      } else {
        setError(messageOf(caught, 'Devam kaydedilemedi. Lütfen tekrar deneyin.'))
      }
    } finally {
      setPending(null)
    }
  }

  const phase = phaseOf(status)
  const showNoAgent = status?.canContinueWithoutAgent === true && status.canContinueIndicated !== true

  return (
    <section className="step-panel" aria-labelledby="agent-step-title">
      <header>
        <h2 id="agent-step-title">Güvenlik ajanını çalıştırın</h2>
        <p>
          Ajan yalnızca sınav süresince çalışır; sanal makine, uzak masaüstü, VPN ve yasaklı uygulamaları denetler. Kişisel dosyalarınız taranmaz, süreç listeniz
          sunucuya gönderilmez.
        </p>
      </header>

      <AgentSetupGuide />

      <div className={`agent-status agent-status-${phase}`} role="status" aria-live="polite">
        {phase === 'indicated' ? (
          <TriangleAlert size={22} aria-hidden="true" />
        ) : phase === 'checking' ? (
          <CircleCheck size={22} aria-hidden="true" />
        ) : (
          <LoaderCircle size={22} className="spinner" aria-hidden="true" />
        )}
        <div>
          <p className="agent-status-title">
            {phase === 'indicated'
              ? 'Sınav kurallarına aykırı göstergeler var'
              : phase === 'checking'
                ? 'Ajan eşleşti, cihaz denetleniyor'
                : 'Ajan bekleniyor'}
          </p>
          <p className="agent-status-text">
            {phase === 'indicated'
              ? 'Aşağıdakileri düzeltip yeniden kontrol edin. Düzeltemiyorsanız gerekçe yazarak devam edebilirsiniz.'
              : phase === 'checking'
                ? 'Sorun yoksa birkaç saniye içinde sonraki adıma geçilir.'
                : 'Ajanı çalıştırıp eşleştirin. Durum birkaç saniyede bir yenilenir.'}
          </p>
        </div>
      </div>

      {phase === 'indicated' ? (
        <ul className="indicator-items" aria-label="Ajan uyarıları">
          {status!.indicators.map((indicator) => (
            <li key={`${indicator.type}:${indicator.name}`}>
              <p className="indicator-message">{indicator.message}</p>
              <p className="indicator-fix">{indicator.fix}</p>
            </li>
          ))}
        </ul>
      ) : null}

      {error ? <Alert tone="danger">{error}</Alert> : null}

      <div className="step-actions step-actions-start">
        {phase === 'indicated' ? (
          <Button loading={pending === 'refresh'} onClick={() => void recheck()} icon={<RefreshCw size={18} aria-hidden="true" />}>
            Düzelttim, tekrar kontrol et
          </Button>
        ) : (
          <Button loading={pending === 'pair'} disabled={pending !== null} onClick={() => void pair()} icon={<Link2 size={18} aria-hidden="true" />}>
            {pending === 'pair' ? 'Eşleştiriliyor…' : 'Ajanla eşleştir'}
          </Button>
        )}
      </div>

      {status?.canContinueIndicated ? (
        <JustificationForm
          title="Gerekçeyle devam"
          description="Uyarıyı gideremiyorsanız gerekçenizi yazın. Gerekçe ve göstergeler oturumunuza işlenir; sınavınız bir değerlendirici tarafından incelenir."
          submitLabel="Gerekçeyle devam et"
          pending={pending === 'continue'}
          onSubmit={(reason) => void proceed('indicated', reason)}
        />
      ) : null}
      {showNoAgent ? (
        <JustificationForm
          title="Ajan olmadan devam"
          description="Ajan bu bilgisayarda çalışmıyorsa gerekçenizi yazarak devam edebilirsiniz. Cihaz denetimi yapılmaz ve sınavınız incelemeye alınır."
          submitLabel="Ajansız devam et"
          pending={pending === 'continue'}
          onSubmit={(reason) => void proceed('noAgent', reason)}
        />
      ) : null}
    </section>
  )
}
