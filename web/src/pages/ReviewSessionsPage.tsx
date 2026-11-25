import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import {
  SESSION_READY_MESSAGE,
  createReviewConnection,
  getReviewExam,
  listReviewSessions,
  type ExamSummary,
  type ReviewSessionList,
  type ReviewSessionRow,
  type ReviewSort,
  type SessionFlag,
  type SessionReadyMessage,
} from '../api/review'
import { ApiError } from '../api/client'
import { FormAlert } from '../components/AuthCard'
import { StatusBadge } from '../components/StatusBadge'
import { Table, type Column } from '../components/Table'
import { useToast } from '../components/useToast'
import { ScoreBar } from '../review/ScoreBar'
import {
  DECISION_LABEL,
  DECISION_TONE,
  FLAGS,
  FLAG_LABEL,
  FLAG_TONE,
  SORT_LABEL,
  STATUS_LABEL,
  STATUS_TONE,
  formatDateTime,
} from '../review/labels'

type RiskFilter = 'all' | 'risky' | 'notRisky'

const RISK_FILTER_LABEL: Record<RiskFilter, string> = {
  all: 'Hepsi',
  risky: 'Yalnızca riskli',
  notRisky: 'Riskli olmayanlar',
}

const SORTS: ReviewSort[] = ['Priority', 'Score', 'StartedAt']

function riskParam(filter: RiskFilter): boolean | undefined {
  return filter === 'all' ? undefined : filter === 'risky'
}

/** Sınavın oturum listesi: karar bekleyen sayısı, işaret rozetleri, filtre ve sıralama (FR-27, FR-29). */
export function ReviewSessionsPage() {
  const { examId } = useParams()
  const showToast = useToast()
  const [exam, setExam] = useState<ExamSummary | null>(null)
  const [list, setList] = useState<ReviewSessionList | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [risk, setRisk] = useState<RiskFilter>('all')
  const [flags, setFlags] = useState<SessionFlag[]>([])
  const [sort, setSort] = useState<ReviewSort>('Priority')
  // Yeni oturum bildirimi gelince artar; listeyi yeniden çektirir (mesajda aday adı yok).
  const [refreshTick, setRefreshTick] = useState(0)

  useEffect(() => {
    if (!examId) return
    const controller = new AbortController()
    getReviewExam(examId)
      .then((summary) => {
        if (!controller.signal.aborted) setExam(summary)
      })
      .catch(() => {
        // Başlık yalnızca bilgi; liste ucu 404'ü ayrıca bildirir.
      })
    return () => controller.abort()
  }, [examId])

  useEffect(() => {
    if (!examId) return
    const controller = new AbortController()
    listReviewSessions(examId, { risky: riskParam(risk), flags, sort }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return
        setList(result)
        setError(null)
      })
      .catch((reason: unknown) => {
        if (controller.signal.aborted || (reason instanceof DOMException && reason.name === 'AbortError')) return
        setError(reason instanceof ApiError ? reason.message : 'Oturumlar yüklenemedi.')
      })
    return () => controller.abort()
  }, [examId, risk, flags, sort, refreshTick])

  useEffect(() => {
    if (!examId) return
    const connection = createReviewConnection()
    connection.on(SESSION_READY_MESSAGE, (message: SessionReadyMessage) => {
      if (message.examId !== examId) return
      showToast('İncelemeye yeni bir oturum düştü.', 'info')
      setRefreshTick((tick) => tick + 1)
    })
    // Bildirim yalnızca kolaylık (K-31): bağlanamazsa liste yine açılıyor, sayfa yenilenince tekrar denenir.
    connection.start().catch(() => undefined)
    return () => {
      void connection.stop()
    }
  }, [examId, showToast])

  const toggleFlag = (flag: SessionFlag) =>
    setFlags((current) => (current.includes(flag) ? current.filter((item) => item !== flag) : [...current, flag]))

  const maxScore = Math.max(0, ...(list?.sessions.map((row) => row.riskScore) ?? []))

  const columns: Column<ReviewSessionRow>[] = [
    {
      key: 'candidate',
      header: 'Aday',
      render: (row) => <Link to={`/review/sessions/${row.sessionId}`}>{row.candidateName}</Link>,
    },
    { key: 'startedAt', header: 'Tarih', render: (row) => formatDateTime(row.startedAt) },
    {
      key: 'score',
      header: 'Risk skoru',
      render: (row) => <ScoreBar score={row.riskScore} risky={row.flags.includes('Risky')} max={maxScore} />,
    },
    {
      key: 'flags',
      header: 'İşaretler',
      render: (row) =>
        row.flags.length === 0 ? (
          '—'
        ) : (
          <span className="badge-list">
            {row.flags.map((flag) => (
              <StatusBadge key={flag} tone={FLAG_TONE[flag]}>
                {FLAG_LABEL[flag]}
              </StatusBadge>
            ))}
          </span>
        ),
    },
    {
      key: 'status',
      header: 'Durum',
      render: (row) => (
        <span className="badge-list">
          <StatusBadge tone={STATUS_TONE[row.status]}>{STATUS_LABEL[row.status]}</StatusBadge>
          {row.decision ? <StatusBadge tone={DECISION_TONE[row.decision]}>{DECISION_LABEL[row.decision]}</StatusBadge> : null}
        </span>
      ),
    },
    { key: 'reason', header: 'Kısa gerekçe', render: (row) => row.topReason ?? '—' },
  ]

  return (
    <section className="card review-wide">
      <p>
        <Link to="/review">← Sınavlar</Link>
      </p>
      <h1>{exam?.name ?? 'Oturumlar'}</h1>
      {list ? (
        <p className="review-pending" aria-live="polite">
          <strong>{list.pendingCount}</strong> oturum karar bekliyor
        </p>
      ) : null}

      <div className="review-filters">
        <div className="field">
          <label htmlFor="review-risk">Risk</label>
          <select id="review-risk" value={risk} onChange={(event) => setRisk(event.target.value as RiskFilter)}>
            {(Object.keys(RISK_FILTER_LABEL) as RiskFilter[]).map((key) => (
              <option key={key} value={key}>
                {RISK_FILTER_LABEL[key]}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="review-sort">Sıralama</label>
          <select id="review-sort" value={sort} onChange={(event) => setSort(event.target.value as ReviewSort)}>
            {SORTS.map((key) => (
              <option key={key} value={key}>
                {SORT_LABEL[key]}
              </option>
            ))}
          </select>
        </div>
        <fieldset className="review-flag-filter">
          <legend>İşaret (en az biri)</legend>
          {FLAGS.map((flag) => (
            <label key={flag} className="check">
              <input type="checkbox" checked={flags.includes(flag)} onChange={() => toggleFlag(flag)} />
              <span>{FLAG_LABEL[flag]}</span>
            </label>
          ))}
        </fieldset>
      </div>

      <FormAlert message={error} />
      {list === null ? (
        error ? null : <p>Yükleniyor…</p>
      ) : (
        <Table rows={list.sessions} rowKey={(row) => row.sessionId} columns={columns} empty="Bu süzgece uyan oturum yok." />
      )}
    </section>
  )
}
