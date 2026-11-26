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
import { docNumber, formatDateTime } from '../../lib/format'
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
  const { data, error, loading, reload } = useResource(loadExams, 'Sınavlar yüklenemedi.')
  const totalPending = data?.reduce((sum, item) => sum + item.queue.pendingCount, 0) ?? 0

  return (
    <div className="review-page">
      <PageHeader
        breadcrumbs={[{ label: 'Değerlendirici paneli' }]}
        title="İncelemeler"
        info={
          <ul>
            <li>Değerlendirici olarak atandığınız sınavlar burada.</li>
            <li>"Bekleyen", kararınızı bekleyen riskli ya da işaretli dosya sayısıdır.</li>
            <li>Bir sınava tıklayınca inceleme paneli açılır.</li>
          </ul>
        }
        description={data ? (totalPending > 0 ? `${totalPending} dosya kararınızı bekliyor.` : 'Karar bekleyen dosya yok.') : 'Size atanmış sınavlar ve inceleme kuyrukları.'}
      />

      {error ? (
        <Alert tone="danger" title="Sınavlar yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="review-exam-list" aria-busy="true">
          <Skeleton height="7rem" radius="lg" />
          <Skeleton height="7rem" radius="lg" />
        </div>
      ) : data && data.length === 0 ? (
        <EmptyState icon={ClipboardCheck} title="Atanmış sınav yok">
          Yönetici sizi bir sınava değerlendirici olarak atadığında burada görünür.
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
                      <dt>Bekleyen</dt>
                      <dd className={queue.pendingCount > 0 ? 'is-accent' : undefined}>{queue.pendingCount}</dd>
                    </div>
                    <div>
                      <dt>Tamamlanan</dt>
                      <dd>{completed}</dd>
                    </div>
                    <div>
                      <dt>Karara bağlanan</dt>
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
