import { ArrowRight, CircleCheck, ClipboardCopy, Clock3, Maximize, MicOff, Send, Wifi } from 'lucide-react'
import { useCallback } from 'react'
import { getExam } from '../../api/exams'
import { getAgentStatus, type SessionView } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import './ReadyStep.css'

const RULE_ICONS = [Maximize, ClipboardCopy, MicOff, Wifi, Send] as const

const messages = defineMessages({
  tr: {
    // Sınav sırasında gerçekten uygulanan kurallar (S4 K-30, E4 K-24, K-16).
    rules: [
      { title: 'Tam ekran ve sekme', text: 'Tam ekrandan çıkmak ya da başka sekmeye geçmek olay olarak kaydedilir.' },
      { title: 'Kopyala-yapıştır ve sağ tık', text: 'Bu işlemler engellenmez ama kaydedilir.' },
      { title: 'Kamera ve mikrofon izni', text: 'İzni kaldırırsanız sorular gizlenir; izinsiz geçen süre kaydedilir.' },
      { title: 'Bağlantı kesilirse', text: 'Süre sunucuda işlemeye devam eder. Döndüğünüzde kimliğiniz yeniden doğrulanır.' },
      { title: 'Süre dolunca', text: 'Sınav otomatik teslim edilir; o ana kadar kaydedilen cevaplar geçerlidir.' },
    ],
    title: 'Sınava başlamadan önce',
    text: 'Kuralları şimdi okuyun; süreniz henüz işlemiyor.',
    duration: 'Süre',
    questions: 'Soru',
    latestStart: 'Son başlangıç',
    done: 'Tamamlanan adımlar',
    consented: 'Açık rıza verildi',
    noAgent: 'Ajan olmadan devam gerekçesi kaydedildi',
    paired: 'Güvenlik ajanı eşleşti',
    during: 'Sınav sırasında',
    timerTitle: 'Süre kimliğiniz doğrulanınca başlar',
    timerText: (minutes: number) =>
      `Sonraki adımda yüzünüz kayıt fotoğrafınızla karşılaştırılır. Doğrulama başarılı olduğu anda sınav hakkınız kullanılmış sayılır ve ${minutes} dakikalık süre başlar; süre durdurulamaz.`,
    next: 'Kimlik doğrulamaya geç',
  },
  en: {
    rules: [
      { title: 'Full screen and tabs', text: 'Leaving full screen or switching to another tab is recorded as an event.' },
      { title: 'Copy-paste and right click', text: 'These actions are not blocked, but they are recorded.' },
      { title: 'Camera and microphone permission', text: 'If you revoke permission, the questions are hidden and the time without permission is recorded.' },
      { title: 'If the connection drops', text: 'The timer keeps running on the server. When you return, your identity is verified again.' },
      { title: 'When time runs out', text: 'The exam is submitted automatically; answers saved until then count.' },
    ],
    title: 'Before you start',
    text: 'Read the rules now; your time is not running yet.',
    duration: 'Duration',
    questions: 'Questions',
    latestStart: 'Latest start',
    done: 'Completed steps',
    consented: 'Explicit consent given',
    noAgent: 'Justification for continuing without the agent saved',
    paired: 'Security agent paired',
    during: 'During the exam',
    timerTitle: 'The timer starts once your identity is verified',
    timerText: (minutes: number) =>
      `In the next step your face is compared with your reference photo. As soon as verification succeeds your attempt is used and the ${minutes}-minute timer starts; it cannot be paused.`,
    next: 'Go to identity check',
  },
})

interface ReadyData {
  questionCount: number | null
  latestStartAt: string | null
  agent: 'paired' | 'without-agent'
}

/** Sınava hazırlık özeti ve kurallar (Stitch ekran 13). Süre bu adımda başlamaz; kimlik doğrulamadan sonra başlar. */
export function ReadyStep({ session, onContinue }: { session: SessionView; onContinue: () => void }) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const load = useCallback(
    async (signal: AbortSignal): Promise<ReadyData> => {
      const [exam, agent] = await Promise.all([getExam(session.examId, signal).catch(() => undefined), getAgentStatus(session.id).catch(() => undefined)])
      return {
        questionCount: exam?.questionCount ?? null,
        latestStartAt: exam?.latestStartAt ?? null,
        agent: agent?.paired ? 'paired' : 'without-agent',
      }
    },
    [session.examId, session.id],
  )
  const { data } = useResource(load)

  return (
    <section className="step-panel" aria-labelledby="ready-step-title">
      <header>
        <h2 id="ready-step-title">{m.title}</h2>
        <p>{m.text}</p>
      </header>

      <dl className="ready-facts">
        <div>
          <dt>{m.duration}</dt>
          <dd className="numeric">{c.minutes(session.durationMinutes)}</dd>
        </div>
        <div>
          <dt>{m.questions}</dt>
          <dd className="numeric">{data ? (data.questionCount ?? '—') : <Skeleton height="1.5rem" width="3rem" />}</dd>
        </div>
        <div>
          <dt>{m.latestStart}</dt>
          <dd className="numeric">{data ? (data.latestStartAt ? formatDateTime(data.latestStartAt) : '—') : <Skeleton height="1.5rem" width="8rem" />}</dd>
        </div>
      </dl>

      <ul className="ready-checks" aria-label={m.done}>
        <li>
          <CircleCheck size={18} aria-hidden="true" />
          {m.consented}
        </li>
        <li>
          <CircleCheck size={18} aria-hidden="true" />
          {data?.agent === 'without-agent' ? m.noAgent : m.paired}
        </li>
      </ul>

      <div>
        <h3 className="ready-rules-title">{m.during}</h3>
        <ul className="ready-rules">
          {m.rules.map(({ title, text }, index) => {
            const Icon = RULE_ICONS[index]
            return (
            <li key={title}>
              <Icon size={20} aria-hidden="true" />
              <span>
                <strong>{title}</strong>
                <span>{text}</span>
              </span>
            </li>
            )
          })}
        </ul>
      </div>

      <Alert tone="warning" title={m.timerTitle} live={false}>
        <p>{m.timerText(session.durationMinutes)}</p>
      </Alert>

      <div className="step-actions">
        <Button size="lg" onClick={onContinue} iconEnd={<ArrowRight size={18} aria-hidden="true" />} icon={<Clock3 size={18} aria-hidden="true" />}>
          {m.next}
        </Button>
      </div>
    </section>
  )
}
