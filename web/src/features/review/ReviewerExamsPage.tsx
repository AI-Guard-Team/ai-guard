import { ArrowRight, ClipboardCheck } from 'lucide-react'
import { Link } from 'react-router'
import { EXAM_TYPE_LABEL, listExams, type ExamSummary } from '../../api/exams'
import { getReviewSessions, type ReviewSessionList } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { docNumber, formatDateTime } from '../../lib/format'

const messages = defineMessages({
  tr: {
    failed: 'Sınavlar yüklenemedi.',
    workspace: 'Değerlendirici paneli',
    title: 'İncelemeler',
    info: [
      'Değerlendirici olarak atandığınız sınavlar burada.',
      '"Bekleyen", kararınızı bekleyen riskli ya da işaretli dosya sayısıdır.',
      'Bir sınava tıklayınca inceleme paneli açılır.',
    ],
    waiting: (n: number) => `${n} dosya kararınızı bekliyor.`,
    nothing: 'Karar bekleyen dosya yok.',
    intro: 'Size atanmış sınavlar ve inceleme kuyrukları.',
    listFailed: 'Sınavlar yüklenemedi',
    none: 'Atanmış sınav yok',
    noneText: 'Yönetici sizi bir sınava değerlendirici olarak atadığında burada görünür.',
    pending: 'Bekleyen',
    completed: 'Tamamlanan',
    decided: 'Karara bağlanan',
  },
  en: {
    failed: 'Exams could not be loaded.',
    workspace: 'Reviewer workspace',
    title: 'Reviews',
    info: [
      'The exams you are assigned to as a reviewer are listed here.',
      '"Pending" is the number of risky or flagged files waiting for your decision.',
      'Click an exam to open its review panel.',
    ],
    waiting: (n: number) => `${n} ${n === 1 ? 'file is' : 'files are'} waiting for your decision.`,
    nothing: 'No files are waiting for a decision.',
    intro: 'Exams assigned to you and their review queues.',
    listFailed: 'Exams could not be loaded',
    none: 'No assigned exams',
    noneText: 'When an administrator assigns you to an exam as a reviewer, it appears here.',
    pending: 'Pending',
    completed: 'Completed',
    decided: 'Decided',
  },
})
import './review.css'

interface ExamWithQueue {
  exam: ExamSummary
  queue: ReviewSessionList
}

// Değerlendiriciye yalnızca atandığı sınavlar dönüyor (E3); her sınavın inceleme kuyruğu paralel çekiliyor.
const loadExams = async (signal: AbortSignal): Promise<ExamWithQueue[]> => {
  const exams = await listExams(signal)
  return Promise.all(exams.map(async (exam) => ({ exam, queue: await getReviewSessions(exam.id, {}, signal) })))
}

/** Değerlendiricinin sınavları ve karar bekleyen dosya sayıları (Stitch ekran 18). */
export function ReviewerExamsPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error, loading, reload } = useResource(loadExams, m.failed)
  const totalPending = data?.reduce((sum, item) => sum + item.queue.pendingCount, 0) ?? 0

  return (
    <div className="review-page">
      <PageHeader
        breadcrumbs={[{ label: m.workspace }]}
        title={m.title}
        info={
          <ul>
            {m.info.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        }
        description={data ? (totalPending > 0 ? m.waiting(totalPending) : m.nothing) : m.intro}
      />

      {error ? (
        <Alert tone="danger" title={m.listFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="review-exam-list" aria-busy="true">
          <Skeleton height="7rem" radius="lg" />
          <Skeleton height="7rem" radius="lg" />
        </div>
      ) : data && data.length === 0 ? (
        <EmptyState icon={ClipboardCheck} title={m.none}>
          {m.noneText}
        </EmptyState>
      ) : data ? (
        <ul className="review-exam-list">
          {data.map(({ exam, queue }) => {
            const completed = queue.sessions.filter((row) => row.status !== 'NotStarted').length
            const decided = queue.sessions.filter((row) => row.status === 'Decided').length
            return (
              <li key={exam.id}>
                <Link className="review-exam-row" to={`/review/exams/${exam.id}`}>
                  <div className="review-exam-main">
                    <p className="kicker">
                      <span className="doc-card-no">{docNumber(exam.id, exam.windowStartAt)}</span> · {EXAM_TYPE_LABEL[exam.type]}
                    </p>
                    <h2>{exam.name}</h2>
                    <p className="review-exam-window">
                      {formatDateTime(exam.windowStartAt)} – {formatDateTime(exam.windowEndAt)}
                    </p>
                  </div>
                  <dl className="stat-strip">
                    <div>
                      <dt>{m.pending}</dt>
                      <dd className={queue.pendingCount > 0 ? 'is-accent' : undefined}>{queue.pendingCount}</dd>
                    </div>
                    <div>
                      <dt>{m.completed}</dt>
                      <dd>{completed}</dd>
                    </div>
                    <div>
                      <dt>{m.decided}</dt>
                      <dd>{decided}</dd>
                    </div>
                  </dl>
                  <ArrowRight className="review-exam-arrow" size={20} aria-hidden="true" />
                </Link>
              </li>
            )
          })}
        </ul>
      ) : null}
    </div>
  )
}
