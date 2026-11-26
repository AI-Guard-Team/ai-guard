import { ClipboardCheck, Files, FolderOpen, Gavel } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router'
import { getExam } from '../../api/exams'
import { getReviewSessions, type ReviewListFilter, type ReviewSessionRow, type ReviewSort, type SessionFlag } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { RiskScore } from '../../components/RiskScore'
import { Seal } from '../../components/Seal'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { DECISION_LABEL, DECISION_TONE, FLAG_LABEL, FLAG_TONE, STATUS_LABEL, STATUS_TONE } from '../../lib/eventCatalog'
import { docNumber, formatStamp, formatTimeRange } from '../../lib/format'
import { StatTile } from '../overview/OverviewParts'
import '../overview/overview.css'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { useReviewHub } from './useReviewHub'
import './review.css'

type FilterKey = 'all' | 'risky' | SessionFlag

const FILTER_KEYS: FilterKey[] = ['all', 'risky', 'NoAgent', 'Indicated', 'PermissionGap', 'MonitoringGap']
const SORT_KEYS: ReviewSort[] = ['Priority', 'Score', 'StartedAt']

const messages = defineMessages({
  tr: {
    filters: { all: 'Tümü', risky: 'Riskli', NoAgent: 'Ajansız', Indicated: 'Göstergeli', PermissionGap: 'İzin boşluğu', MonitoringGap: 'İzleme boşluğu', Risky: 'Riskli' } as Record<FilterKey, string>,
    sorts: { Priority: 'Öncelik', Score: 'Risk skoru', StartedAt: 'Başlangıç' } as Record<ReviewSort, string>,
    failed: 'Dosyalar yüklenemedi.',
    newFile: 'Yeni bir dosya incelemeye düştü.',
    reviews: 'İncelemeler',
    panel: 'İnceleme paneli',
    description: 'Dosyayı açıp olaylara bakın, yanlış alarmları ayıklayın ve karar verin.',
    info: [
      'Bu sınavda tamamlanan oturumların dosyaları burada. Karar bekleyenler varsayılan olarak en üstte.',
      'Filtrelerle yalnızca riskli ya da belirli işaretli dosyaları görebilirsiniz.',
      'Bir dosyaya tıklayınca olaylar, skor gerekçesi ve karar ekranı açılır.',
    ],
    waiting: 'dosya kararınızı bekliyor',
    completed: 'Tamamlanan',
    pending: 'Karar bekleyen',
    decided: 'Karara bağlanan',
    total: 'Toplam dosya',
    filter: 'Filtre',
    sort: 'Sırala',
    listFailed: 'Dosyalar yüklenemedi',
    none: 'Henüz dosya yok',
    noneForFilter: 'Bu filtreye uyan dosya yok',
    noneText: 'Adaylar sınavı tamamladıkça dosyalar burada görünür.',
    noneForFilterText: 'Başka bir filtre seçin ya da "Tümü"ne dönün.',
    noReason: 'Skora katkı yapan olay yok.',
    decision: (label: string) => `Karar: ${label}`,
  },
  en: {
    filters: { all: 'All', risky: 'Risky', NoAgent: 'No agent', Indicated: 'Indicators', PermissionGap: 'Permission gap', MonitoringGap: 'Monitoring gap', Risky: 'Risky' },
    sorts: { Priority: 'Priority', Score: 'Risk score', StartedAt: 'Start time' },
    failed: 'Files could not be loaded.',
    newFile: 'A new file has arrived for review.',
    reviews: 'Reviews',
    panel: 'Review panel',
    description: 'Open a file, look at the events, weed out false alarms and decide.',
    info: [
      'Files of completed sessions in this exam are listed here. Those awaiting a decision are at the top by default.',
      'Use the filters to see only risky or specifically flagged files.',
      'Click a file to open its events, score explanation and decision screen.',
    ],
    waiting: 'files waiting for your decision',
    completed: 'Completed',
    pending: 'Awaiting decision',
    decided: 'Decided',
    total: 'Total files',
    filter: 'Filter',
    sort: 'Sort',
    listFailed: 'Files could not be loaded',
    none: 'No files yet',
    noneForFilter: 'No files match this filter',
    noneText: 'Files appear here as candidates complete the exam.',
    noneForFilterText: 'Choose another filter or go back to "All".',
    noReason: 'No event contributed to the score.',
    decision: (label: string) => `Decision: ${label}`,
  },
})

function toQuery(filter: FilterKey, sort: ReviewSort): ReviewListFilter {
  if (filter === 'all') return { sort }
  if (filter === 'risky') return { risky: true, sort }
  return { flags: [filter], sort }
}

/**
 * İnceleme paneli, "vaka dosyaları" (Stitch 19, Prompt E): başlıkta bekleyen dosya sayısı, tek satırlık istatistik şeridi,
 * filtre çipleri ve dosya satırları. Sıralama ve filtre sunucuda (K-31); karar bekleyenler varsayılan olarak önde.
 */
export function ReviewPanelPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { examId = '' } = useParams()
  const showToast = useToast()
  const [filter, setFilter] = useState<FilterKey>('all')
  const [sort, setSort] = useState<ReviewSort>('Priority')

  const load = useCallback(
    async (signal: AbortSignal) => {
      // Dosya listesinin hatası öne alınır: sunucunun mesajı (ör. "bu sınava atanmadınız") sınav isteğininkinden açıklayıcı.
      const [exam, list] = await Promise.allSettled([getExam(examId, signal), getReviewSessions(examId, toQuery(filter, sort), signal)])
      if (list.status === 'rejected') throw list.reason
      return { exam: exam.status === 'fulfilled' ? exam.value : undefined, list: list.value }
    },
    [examId, filter, sort],
  )
  const { data, error, loading, reload } = useResource(load, m.failed)

  useReviewHub((message) => {
    if (message.examId !== examId) return
    showToast(m.newFile, 'info')
    reload()
  })

  const rows = data?.list.sessions ?? []
  const completed = rows.filter((row) => row.status !== 'NotStarted').length
  const decided = rows.filter((row) => row.status === 'Decided').length
  const pending = data?.list.pendingCount ?? 0

  return (
    <div className="review-page">
      <PageHeader
        breadcrumbs={[{ label: m.reviews, to: '/review' }]}
        title={data?.exam?.name ?? m.panel}
        description={m.description}
        info={
          <ul>
            {m.info.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        }
        actions={
          <p className="review-pending" aria-live="polite">
            <span className="review-pending-count numeric">{data ? pending : '–'}</span>
            <span>{m.waiting}</span>
          </p>
        }
      />

      <ul className="stat-grid">
        <StatTile icon={ClipboardCheck} tone="sky" value={data ? completed : '–'} label={m.completed} />
        <StatTile icon={FolderOpen} tone="amber" value={data ? pending : '–'} label={m.pending} index={1} />
        <StatTile icon={Gavel} tone="mint" value={data ? decided : '–'} label={m.decided} index={2} />
        <StatTile icon={Files} tone="lilac" value={data ? rows.length : '–'} label={m.total} index={3} />
      </ul>

      <div className="review-toolbar">
        <div className="chip-tabs" role="group" aria-label={m.filter}>
          {FILTER_KEYS.map((key) => (
            <button key={key} type="button" className="chip-tab" aria-pressed={filter === key} onClick={() => setFilter(key)}>
              {m.filters[key]}
            </button>
          ))}
        </div>
        <label className="review-sort">
          <span className="kicker">{m.sort}</span>
          <select value={sort} onChange={(event) => setSort(event.target.value as ReviewSort)}>
            {SORT_KEYS.map((key) => (
              <option key={key} value={key}>
                {m.sorts[key]}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title={m.listFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="case-list" aria-busy="true">
          <Skeleton height="6rem" />
          <Skeleton height="6rem" />
          <Skeleton height="6rem" />
        </div>
      ) : data && rows.length === 0 ? (
        <EmptyState icon={FolderOpen} title={filter === 'all' ? m.none : m.noneForFilter}>
          {filter === 'all' ? m.noneText : m.noneForFilterText}
        </EmptyState>
      ) : (
        <ol className="case-list">
          {rows.map((row) => (
            <li key={row.sessionId}>
              <CaseRow row={row} />
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

function CaseRow({ row }: { row: ReviewSessionRow }) {
  const m = useMessages(messages)
  const visibleFlags = row.flags.filter((flag) => flag !== 'Risky')
  return (
    <Link className={['case-row', row.status === 'AwaitingReview' && 'is-pending'].filter(Boolean).join(' ')} to={`/review/sessions/${row.sessionId}`}>
      <div className="case-row-id">
        <p className="kicker">
          <span className="doc-card-no">{docNumber(row.sessionId, row.startedAt)}</span> · {formatStamp(row.startedAt).slice(0, 10)}{' '}
          {formatTimeRange(row.startedAt, row.endedAt)}
        </p>
        <p className="case-row-name">{row.candidateName}</p>
        <p className="case-row-reason">{row.topReason ?? m.noReason}</p>
      </div>
      <div className="case-row-flags">
        <StatusBadge tone={STATUS_TONE[row.status]} dot>
          {STATUS_LABEL[row.status]}
        </StatusBadge>
        {visibleFlags.map((flag) => (
          <StatusBadge key={flag} tone={FLAG_TONE[flag]} stamp>
            {FLAG_LABEL[flag]}
          </StatusBadge>
        ))}
      </div>
      <RiskScore score={row.riskScore} risky={row.flags.includes('Risky')} />
      <div className="case-row-seal">
        {row.decision ? <Seal label={DECISION_LABEL[row.decision]} tone={DECISION_TONE[row.decision]} size={64} title={m.decision(DECISION_LABEL[row.decision])} /> : null}
      </div>
    </Link>
  )
}
