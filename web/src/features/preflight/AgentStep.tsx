import { CircleCheck, Link2, LoaderCircle, RefreshCw, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { PairingError, pairAgent } from '../../api/agentPairing'
import { ApiError } from '../../api/client'
import { continueAgent, type AgentContinueMode, type AgentStatus } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AgentSetupGuide } from './AgentSetupGuide'
import { JustificationForm } from './JustificationForm'
import { useAgentStatus } from './useAgentStatus'
import './AgentStep.css'

const messages = defineMessages({
  tr: {
    pairFailed: 'Ajan eşleştirilemedi. Lütfen tekrar deneyin.',
    noAgentBlocked: 'Ajansız devam edilemiyor.',
    continueFailed: 'Devam kaydedilemedi. Lütfen tekrar deneyin.',
    title: 'Güvenlik ajanını çalıştırın',
    text: 'Ajan yalnızca sınav süresince çalışır; sanal makine, uzak masaüstü, VPN ve yasaklı uygulamaları denetler. Kişisel dosyalarınız taranmaz, süreç listeniz sunucuya gönderilmez.',
    indicatedTitle: 'Sınav kurallarına aykırı göstergeler var',
    checkingTitle: 'Ajan eşleşti, cihaz denetleniyor',
    waitingTitle: 'Ajan bekleniyor',
    indicatedText: 'Aşağıdakileri düzeltip yeniden kontrol edin. Düzeltemiyorsanız gerekçe yazarak devam edebilirsiniz.',
    checkingText: 'Sorun yoksa birkaç saniye içinde sonraki adıma geçilir.',
    waitingText: 'Ajanı çalıştırıp eşleştirin. Durum birkaç saniyede bir yenilenir.',
    warnings: 'Ajan uyarıları',
    recheck: 'Düzelttim, tekrar kontrol et',
    pairing: 'Eşleştiriliyor…',
    pair: 'Ajanla eşleştir',
    indicatedFormTitle: 'Gerekçeyle devam',
    indicatedFormText: 'Uyarıyı gideremiyorsanız gerekçenizi yazın. Gerekçe ve göstergeler oturumunuza işlenir; sınavınız bir değerlendirici tarafından incelenir.',
    indicatedSubmit: 'Gerekçeyle devam et',
    noAgentTitle: 'Ajan olmadan devam',
    noAgentText: 'Ajan bu bilgisayarda çalışmıyorsa gerekçenizi yazarak devam edebilirsiniz. Cihaz denetimi yapılmaz ve sınavınız incelemeye alınır.',
    noAgentSubmit: 'Ajansız devam et',
  },
  en: {
    pairFailed: 'The agent could not be paired. Please try again.',
    noAgentBlocked: 'You cannot continue without the agent.',
    continueFailed: 'Your choice could not be saved. Please try again.',
    title: 'Run the security agent',
    text: 'The agent only runs during the exam and checks for virtual machines, remote desktop, VPN and forbidden apps. Your personal files are not scanned and your process list is not sent to the server.',
    indicatedTitle: 'Indicators against the exam rules were found',
    checkingTitle: 'Agent paired, checking the device',
    waitingTitle: 'Waiting for the agent',
    indicatedText: 'Fix the items below and check again. If you cannot fix them, you can continue by writing a justification.',
    checkingText: 'If everything is fine you move to the next step in a few seconds.',
    waitingText: 'Start the agent and pair it. The status refreshes every few seconds.',
    warnings: 'Agent warnings',
    recheck: 'I fixed it, check again',
    pairing: 'Pairing…',
    pair: 'Pair with agent',
    indicatedFormTitle: 'Continue with a justification',
    indicatedFormText: 'If you cannot resolve the warning, write your justification. It is recorded in your session with the indicators, and your exam is reviewed by a reviewer.',
    indicatedSubmit: 'Continue with justification',
    noAgentTitle: 'Continue without the agent',
    noAgentText: 'If the agent does not run on this computer, you can continue by writing a justification. No device check is made and your exam is reviewed.',
    noAgentSubmit: 'Continue without agent',
  },
})

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
  const m = useMessages(messages)
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
      setError(messageOf(caught, m.pairFailed))
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
        setError(m.noAgentBlocked)
        await refresh()
      } else {
        setError(messageOf(caught, m.continueFailed))
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
        <h2 id="agent-step-title">{m.title}</h2>
        <p>{m.text}</p>
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
            {phase === 'indicated' ? m.indicatedTitle : phase === 'checking' ? m.checkingTitle : m.waitingTitle}
          </p>
          <p className="agent-status-text">
            {phase === 'indicated' ? m.indicatedText : phase === 'checking' ? m.checkingText : m.waitingText}
          </p>
        </div>
      </div>

      {phase === 'indicated' ? (
        <ul className="indicator-items" aria-label={m.warnings}>
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
            {m.recheck}
          </Button>
        ) : (
          <Button loading={pending === 'pair'} disabled={pending !== null} onClick={() => void pair()} icon={<Link2 size={18} aria-hidden="true" />}>
            {pending === 'pair' ? m.pairing : m.pair}
          </Button>
        )}
      </div>

      {status?.canContinueIndicated ? (
        <JustificationForm
          title={m.indicatedFormTitle}
          description={m.indicatedFormText}
          submitLabel={m.indicatedSubmit}
          pending={pending === 'continue'}
          onSubmit={(reason) => void proceed('indicated', reason)}
        />
      ) : null}
      {showNoAgent ? (
        <JustificationForm
          title={m.noAgentTitle}
          description={m.noAgentText}
          submitLabel={m.noAgentSubmit}
          pending={pending === 'continue'}
          onSubmit={(reason) => void proceed('noAgent', reason)}
        />
      ) : null}
    </section>
  )
}
