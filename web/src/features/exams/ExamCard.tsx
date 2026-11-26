import { ArrowRight, CalendarClock, CalendarRange, Clock3, ListChecks } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError } from '../../api/client'
import { EXAM_TYPE_LABEL, startSession, type ExamSummary } from '../../api/exams'
import { Button } from '../../components/Button'
import { StatusBadge } from '../../components/StatusBadge'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime, type ExamStatus } from './examStatus'

const messages = defineMessages({
  tr: {
    startFailed: 'Sınav başlatılamadı. Lütfen tekrar deneyin.',
    info: 'Sınav bilgileri',
    opens: 'Açılış',
    latestStart: 'Son başlangıç',
    closes: 'Kapanış',
    starting: 'Başlatılıyor…',
    start: 'Sınav öncesi adımlara geç',
    resumePreflight: 'Hazırlığa devam et',
    resumeExam: 'Sınava dön',
    uploadPhoto: 'Kayıt fotoğrafı yükle',
  },
  en: {
    startFailed: 'The exam could not be started. Please try again.',
    info: 'Exam details',
    opens: 'Opens',
    latestStart: 'Latest start',
    closes: 'Closes',
    starting: 'Starting…',
    start: 'Go to pre-exam steps',
    resumePreflight: 'Continue preparation',
    resumeExam: 'Back to exam',
    uploadPhoto: 'Upload reference photo',
  },
})
import './ExamCard.css'

const RUNNING_STATES = new Set(['InProgress', 'PermissionPending', 'Disconnected'])

interface ExamCardProps {
  exam: ExamSummary
  status: ExamStatus
}

/** "Sınavlarım" kartı (Stitch ekran 7). Durumu `examStatus` hesaplar; kart yalnızca çizer ve eylemi çalıştırır. */
export function ExamCard({ exam, status }: ExamCardProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
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
      setError(caught instanceof ApiError ? caught.message : m.startFailed)
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

      <ul className="exam-tile-chips" aria-label={m.info}>
        <li>
          <Clock3 size={15} aria-hidden="true" />
          <span className="numeric">{c.minutes(exam.durationMinutes)}</span>
        </li>
        <li>
          <ListChecks size={15} aria-hidden="true" />
          <span className="numeric">{c.questions(exam.questionCount)}</span>
        </li>
      </ul>

      <dl className="exam-tile-window">
        <div>
          <dt>
            <CalendarRange size={15} aria-hidden="true" />
            {m.opens}
          </dt>
          <dd className="numeric">{formatDateTime(exam.windowStartAt)}</dd>
        </div>
        <div>
          <dt>
            <CalendarClock size={15} aria-hidden="true" />
            {m.latestStart}
          </dt>
          <dd className="numeric">{formatDateTime(exam.latestStartAt)}</dd>
        </div>
        <div className="exam-tile-window-end">
          <dt>{m.closes}</dt>
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
            {pending ? m.starting : m.start}
          </Button>
        ) : action.kind === 'resume-preflight' ? (
          <Link className="button button-primary button-md button-block" to={`/exams/${action.sessionId}/start`}>
            {m.resumePreflight}
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        ) : action.kind === 'resume-exam' ? (
          <Link className="button button-primary button-md button-block" to={`/exams/${action.sessionId}`}>
            {m.resumeExam}
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        ) : action.kind === 'upload-photo' ? (
          <Link className="button button-secondary button-md button-block" to="/profile/photo">
            {m.uploadPhoto}
          </Link>
        ) : null}
      </footer>
    </article>
  )
}
