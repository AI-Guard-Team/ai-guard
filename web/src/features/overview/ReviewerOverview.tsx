import { ArrowRight, ClipboardCheck, FolderOpen, Gavel, ListChecks } from 'lucide-react'
import { Link } from 'react-router'
import { listExams } from '../../api/exams'
import { getReviewSessions } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useResource } from '../../hooks/useResource'
import { defineMessages, useMessages } from '../../i18n/messages'
import { greeting } from './greeting'
import { CardHead, Hero, StatTile } from './OverviewParts'

const messages = defineMessages({
  tr: {
    failed: 'Bilgiler yüklenemedi.',
    waiting: (n: number) => `${n} dosya kararınızı bekliyor.`,
    none: 'Karar bekleyen dosya yok. Yeni bir dosya düştüğünde inceleme panelinde görünür.',
    loading: 'Dosyalar yükleniyor…',
    start: 'İncelemeye başla',
    toReviews: 'İncelemelere git',
    pending: 'Karar bekleyen',
    decided: 'Karara bağlanan',
    completed: 'Tamamlanan oturum',
    assigned: 'Atandığınız sınav',
    exams: 'Sınavlarınız',
    noExams: 'Henüz bir sınava değerlendirici olarak atanmadınız.',
    files: (n: number) => `${n} dosya`,
    awaiting: (n: number) => `${n} karar bekliyor`,
    clear: 'Bekleyen yok',
  },
  en: {
    failed: 'The information could not be loaded.',
    waiting: (n: number) => `${n} ${n === 1 ? 'file is' : 'files are'} waiting for your decision.`,
    none: 'No files are waiting for a decision. When a new file arrives, it appears in the review panel.',
    loading: 'Loading files…',
    start: 'Start reviewing',
    toReviews: 'Go to reviews',
    pending: 'Awaiting decision',
    decided: 'Decided',
    completed: 'Completed sessions',
    assigned: 'Assigned exams',
    exams: 'Your exams',
    noExams: 'You have not been assigned to an exam as a reviewer yet.',
    files: (n: number) => `${n} ${n === 1 ? 'file' : 'files'}`,
    awaiting: (n: number) => `${n} awaiting decision`,
    clear: 'Nothing pending',
  },
})

const load = async (signal: AbortSignal) => {
  const exams = await listExams(signal)
  return Promise.all(exams.map(async (exam) => ({ exam, queue: await getReviewSessions(exam.id, {}, signal) })))
}

/** Değerlendirici genel bakışı: bekleyen dosyalar, sınav bazında kuyruklar ve değerlendirmenin adımları. */
export function ReviewerOverview({ firstName }: { firstName: string }) {
  const m = useMessages(messages)
  const { data, error } = useResource(load, m.failed)
  const rows = data ?? []
  const pending = rows.reduce((sum, item) => sum + item.queue.pendingCount, 0)
  const decided = rows.reduce((sum, item) => sum + item.queue.sessions.filter((row) => row.status === 'Decided').length, 0)
  const completed = rows.reduce((sum, item) => sum + item.queue.sessions.filter((row) => row.status !== 'NotStarted').length, 0)
  // Sıradaki dosya: en çok bekleyen sınavın ilk karar bekleyen dosyası (liste sunucuda öncelik sıralı).
  const busiest = [...rows].sort((a, b) => b.queue.pendingCount - a.queue.pendingCount)[0]
  const nextCase = busiest?.queue.sessions.find((row) => row.status === 'AwaitingReview')

  return (
    <div className="overview">
      <Hero
        greeting={greeting(firstName)}
        text={data ? (pending > 0 ? m.waiting(pending) : m.none) : m.loading}
        aside={
          nextCase ? (
            <Link className="button button-lg hero-button" to={`/review/sessions/${nextCase.sessionId}`}>
              {m.start}
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          ) : (
            <Link className="button button-lg hero-button" to="/review">
              {m.toReviews}
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          )
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={FolderOpen} tone="amber" value={data ? pending : '–'} label={m.pending} to="/review" />
        <StatTile icon={Gavel} tone="mint" value={data ? decided : '–'} label={m.decided} index={1} />
        <StatTile icon={ClipboardCheck} tone="sky" value={data ? completed : '–'} label={m.completed} index={2} />
        <StatTile icon={ListChecks} tone="lilac" value={data ? rows.length : '–'} label={m.assigned} to="/review" index={3} />
      </ul>

      <section className="overview-card">
        <CardHead title={m.exams} to="/review" />
        {!data ? (
          <Skeleton height="8rem" />
        ) : rows.length === 0 ? (
          <p className="overview-empty">{m.noExams}</p>
        ) : (
          <ul className="overview-list">
            {rows.map(({ exam, queue }) => (
              <li key={exam.id}>
                <Link to={`/review/exams/${exam.id}`}>
                  <span>
                    <strong>{exam.name}</strong>
                    <span className="overview-sub">{m.files(queue.sessions.length)}</span>
                  </span>
                  {queue.pendingCount > 0 ? (
                    <StatusBadge tone="warning" dot>
                      {m.awaiting(queue.pendingCount)}
                    </StatusBadge>
                  ) : (
                    <StatusBadge tone="success" dot>
                      {m.clear}
                    </StatusBadge>
                  )}
                  <ArrowRight size={18} aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
