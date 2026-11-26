import { ArrowRight, CalendarClock, CalendarRange, Clock3, ListChecks } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError } from '../../api/client'
import { EXAM_TYPE_LABEL, startSession, type ExamSummary } from '../../api/exams'
import { Button } from '../../components/Button'
import { StatusBadge } from '../../components/StatusBadge'
import { formatDateTime, type ExamStatus } from './examStatus'
import './ExamCard.css'

const RUNNING_STATES = new Set(['InProgress', 'PermissionPending', 'Disconnected'])

interface ExamCardProps {
  exam: ExamSummary
  status: ExamStatus
}

/** "Sınavlarım" kartı (Stitch ekran 7). Durumu `examStatus` hesaplar; kart yalnızca çizer ve eylemi çalıştırır. */
export function ExamCard({ exam, status }: ExamCardProps) {
  const navigate = useNavigate()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const titleId = `exam-${exam.id}-title`

  const start = async () => {
    setPending(true)
    setError(null)
    try {
      const result = await startSession(exam.id)
      if (!result) throw new Error()
      navigate(RUNNING_STATES.has(result.state) ? `/exams/${result.sessionId}` : `/exams/${result.sessionId}/start`)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Sınav başlatılamadı. Lütfen tekrar deneyin.')
      setPending(false)
    }
  }

  const { action } = status
  return (
    <article className={`exam-tile exam-tile-${status.kind}`} aria-labelledby={titleId}>
      <header className="exam-tile-header">
        <StatusBadge tone="neutral">{EXAM_TYPE_LABEL[exam.type] ?? exam.type}</StatusBadge>
        <StatusBadge tone={status.tone} dot>
          {status.label}
        </StatusBadge>
      </header>

      <h2 id={titleId} className="exam-tile-title">
        {exam.name}
      </h2>

      <ul className="exam-tile-chips" aria-label="Sınav bilgileri">
        <li>
          <Clock3 size={15} aria-hidden="true" />
          <span className="numeric">{exam.durationMinutes} dakika</span>
        </li>
        <li>
          <ListChecks size={15} aria-hidden="true" />
          <span className="numeric">{exam.questionCount} soru</span>
        </li>
      </ul>

      <dl className="exam-tile-window">
        <div>
          <dt>
            <CalendarRange size={15} aria-hidden="true" />
            Açılış
          </dt>
          <dd className="numeric">{formatDateTime(exam.windowStartAt)}</dd>
        </div>
        <div>
          <dt>
            <CalendarClock size={15} aria-hidden="true" />
            Son başlangıç
          </dt>
          <dd className="numeric">{formatDateTime(exam.latestStartAt)}</dd>
        </div>
        <div className="exam-tile-window-end">
          <dt>Kapanış</dt>
          <dd className="numeric">{formatDateTime(exam.windowEndAt)}</dd>
        </div>
      </dl>

      <footer className="exam-tile-footer">
        {status.note ? <p className="exam-tile-note">{status.note}</p> : null}
        {error ? (
          <p className="exam-tile-error" role="alert">
            {error}
          </p>
        ) : null}
        {action.kind === 'start' ? (
          <Button block loading={pending} onClick={() => void start()} iconEnd={pending ? undefined : <ArrowRight size={18} aria-hidden="true" />}>
            {pending ? 'Başlatılıyor…' : 'Sınav öncesi adımlara geç'}
          </Button>
        ) : action.kind === 'resume-preflight' ? (
          <Link className="button button-primary button-md button-block" to={`/exams/${action.sessionId}/start`}>
            Hazırlığa devam et
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        ) : action.kind === 'resume-exam' ? (
          <Link className="button button-primary button-md button-block" to={`/exams/${action.sessionId}`}>
            Sınava dön
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        ) : action.kind === 'upload-photo' ? (
          <Link className="button button-secondary button-md button-block" to="/profile/photo">
            Kayıt fotoğrafı yükle
          </Link>
        ) : null}
      </footer>
    </article>
  )
}
