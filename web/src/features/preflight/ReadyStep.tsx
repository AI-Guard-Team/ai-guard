import { ArrowRight, CircleCheck, ClipboardCopy, Clock3, Maximize, MicOff, Send, Wifi } from 'lucide-react'
import { useCallback } from 'react'
import { getExam } from '../../api/exams'
import { getAgentStatus, type SessionView } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { formatDateTime } from '../exams/examStatus'
import './ReadyStep.css'

// Sınav sırasında gerçekten uygulanan kurallar (S4 K-30, E4 K-24, K-16).
const RULES = [
  { icon: Maximize, title: 'Tam ekran ve sekme', text: 'Tam ekrandan çıkmak ya da başka sekmeye geçmek olay olarak kaydedilir.' },
  { icon: ClipboardCopy, title: 'Kopyala-yapıştır ve sağ tık', text: 'Bu işlemler engellenmez ama kaydedilir.' },
  { icon: MicOff, title: 'Kamera ve mikrofon izni', text: 'İzni kaldırırsanız sorular gizlenir; izinsiz geçen süre kaydedilir.' },
  { icon: Wifi, title: 'Bağlantı kesilirse', text: 'Süre sunucuda işlemeye devam eder. Döndüğünüzde kimliğiniz yeniden doğrulanır.' },
  { icon: Send, title: 'Süre dolunca', text: 'Sınav otomatik teslim edilir; o ana kadar kaydedilen cevaplar geçerlidir.' },
] as const

interface ReadyData {
  questionCount: number | null
  latestStartAt: string | null
  agent: 'paired' | 'without-agent'
}

/** Sınava hazırlık özeti ve kurallar (Stitch ekran 13). Süre bu adımda başlamaz; kimlik doğrulamadan sonra başlar. */
export function ReadyStep({ session, onContinue }: { session: SessionView; onContinue: () => void }) {
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
        <h2 id="ready-step-title">Sınava başlamadan önce</h2>
        <p>Kuralları şimdi okuyun; süreniz henüz işlemiyor.</p>
      </header>

      <dl className="ready-facts">
        <div>
          <dt>Süre</dt>
          <dd className="numeric">{session.durationMinutes} dakika</dd>
        </div>
        <div>
          <dt>Soru</dt>
          <dd className="numeric">{data ? (data.questionCount ?? '—') : <Skeleton height="1.5rem" width="3rem" />}</dd>
        </div>
        <div>
          <dt>Son başlangıç</dt>
          <dd className="numeric">{data ? (data.latestStartAt ? formatDateTime(data.latestStartAt) : '—') : <Skeleton height="1.5rem" width="8rem" />}</dd>
        </div>
      </dl>

      <ul className="ready-checks" aria-label="Tamamlanan adımlar">
        <li>
          <CircleCheck size={18} aria-hidden="true" />
          Açık rıza verildi
        </li>
        <li>
          <CircleCheck size={18} aria-hidden="true" />
          {data?.agent === 'without-agent' ? 'Ajan olmadan devam gerekçesi kaydedildi' : 'Güvenlik ajanı eşleşti'}
        </li>
      </ul>

      <div>
        <h3 className="ready-rules-title">Sınav sırasında</h3>
        <ul className="ready-rules">
          {RULES.map(({ icon: Icon, title, text }) => (
            <li key={title}>
              <Icon size={20} aria-hidden="true" />
              <span>
                <strong>{title}</strong>
                <span>{text}</span>
              </span>
            </li>
          ))}
        </ul>
      </div>

      <Alert tone="warning" title="Süre kimliğiniz doğrulanınca başlar" live={false}>
        <p>
          Sonraki adımda yüzünüz kayıt fotoğrafınızla karşılaştırılır. Doğrulama başarılı olduğu anda sınav hakkınız kullanılmış sayılır ve{' '}
          {session.durationMinutes} dakikalık süre başlar; süre durdurulamaz.
        </p>
      </Alert>

      <div className="step-actions">
        <Button size="lg" onClick={onContinue} iconEnd={<ArrowRight size={18} aria-hidden="true" />} icon={<Clock3 size={18} aria-hidden="true" />}>
          Kimlik doğrulamaya geç
        </Button>
      </div>
    </section>
  )
}
