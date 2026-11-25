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
import { useReviewHub } from './useReviewHub'
import './review.css'

type FilterKey = 'all' | 'risky' | SessionFlag

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Tümü' },
  { key: 'risky', label: 'Riskli' },
  { key: 'NoAgent', label: 'Ajansız' },
  { key: 'Indicated', label: 'Göstergeli' },
  { key: 'PermissionGap', label: 'İzin boşluğu' },
  { key: 'MonitoringGap', label: 'İzleme boşluğu' },
]

const SORTS: { key: ReviewSort; label: string }[] = [
  { key: 'Priority', label: 'Öncelik' },
  { key: 'Score', label: 'Risk skoru' },
  { key: 'StartedAt', label: 'Başlangıç' },
]

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
  const { examId = '' } = useParams()
  const showToast = useToast()
  const [filter, setFilter] = useState<FilterKey>('all')
  const [sort, setSort] = useState<ReviewSort>('Priority')

  const load = useCallback(
    async (signal: AbortSignal) => {
      const [exam, list] = await Promise.all([getExam(examId, signal), getReviewSessions(examId, toQuery(filter, sort), signal)])
      return { exam, list }
    },
    [examId, filter, sort],
  )
  const { data, error, loading, reload } = useResource(load, 'Dosyalar yüklenemedi.')

  useReviewHub((message) => {
    if (message.examId !== examId) return
    showToast('Yeni bir dosya incelemeye düştü.', 'info')
    reload()
  })

  const rows = data?.list.sessions ?? []
  const completed = rows.filter((row) => row.status !== 'NotStarted').length
  const decided = rows.filter((row) => row.status === 'Decided').length
  const pending = data?.list.pendingCount ?? 0

  return (
    <div className="review-page">
      <PageHeader
        breadcrumbs={[{ label: 'İncelemeler', to: '/review' }]}
        title={data?.exam?.name ?? 'İnceleme paneli'}
        description="Dosyayı açıp olaylara bakın, yanlış alarmları ayıklayın ve karar verin."
        info={
          <ul>
            <li>Bu sınavda tamamlanan oturumların dosyaları burada. Karar bekleyenler varsayılan olarak en üstte.</li>
            <li>Filtrelerle yalnızca riskli ya da belirli işaretli dosyaları görebilirsiniz.</li>
            <li>Bir dosyaya tıklayınca olaylar, skor gerekçesi ve karar ekranı açılır.</li>
          </ul>
        }
        actions={
          <p className="review-pending" aria-live="polite">
            <span className="review-pending-count numeric">{data ? pending : '–'}</span>
            <span>dosya kararınızı bekliyor</span>
          </p>
        }
      />

      <ul className="stat-grid">
        <StatTile icon={ClipboardCheck} tone="sky" value={data ? completed : '–'} label="Tamamlanan" />
        <StatTile icon={FolderOpen} tone="amber" value={data ? pending : '–'} label="Karar bekleyen" index={1} />
        <StatTile icon={Gavel} tone="mint" value={data ? decided : '–'} label="Karara bağlanan" index={2} />
        <StatTile icon={Files} tone="lilac" value={data ? rows.length : '–'} label="Toplam dosya" index={3} />
      </ul>

      <div className="review-toolbar">
        <div className="chip-tabs" role="group" aria-label="Filtre">
          {FILTERS.map((item) => (
            <button key={item.key} type="button" className="chip-tab" aria-pressed={filter === item.key} onClick={() => setFilter(item.key)}>
              {item.label}
            </button>
          ))}
        </div>
        <label className="review-sort">
          <span className="kicker">Sırala</span>
          <select value={sort} onChange={(event) => setSort(event.target.value as ReviewSort)}>
            {SORTS.map((item) => (
              <option key={item.key} value={item.key}>
                {item.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title="Dosyalar yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
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
        <EmptyState icon={FolderOpen} title={filter === 'all' ? 'Henüz dosya yok' : 'Bu filtreye uyan dosya yok'}>
          {filter === 'all' ? 'Adaylar sınavı tamamladıkça dosyalar burada görünür.' : 'Başka bir filtre seçin ya da "Tümü"ne dönün.'}
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
  const visibleFlags = row.flags.filter((flag) => flag !== 'Risky')
  return (
    <Link className={['case-row', row.status === 'AwaitingReview' && 'is-pending'].filter(Boolean).join(' ')} to={`/review/sessions/${row.sessionId}`}>
      <div className="case-row-id">
        <p className="kicker">
          <span className="doc-card-no">{docNumber(row.sessionId, row.startedAt)}</span> · {formatStamp(row.startedAt).slice(0, 10)}{' '}
          {formatTimeRange(row.startedAt, row.endedAt)}
        </p>
        <p className="case-row-name">{row.candidateName}</p>
        <p className="case-row-reason">{row.topReason ?? 'Skora katkı yapan olay yok.'}</p>
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
        {row.decision ? <Seal label={DECISION_LABEL[row.decision]} tone={DECISION_TONE[row.decision]} size={64} title={`Karar: ${DECISION_LABEL[row.decision]}`} /> : null}
      </div>
    </Link>
  )
}
