import { ChevronDown, ScrollText } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useCallback, useState } from 'react'
import { listAuditLogs, type AuditLogView } from '../../api/admin'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { formatDateTime } from '../../lib/format'
import { ACTION_LABEL } from './auditLabels'
import './admin.css'

const PAGE_SIZE = 25

const TARGET_LABEL: Record<string, string> = {
  User: 'Kullanıcı',
  Exam: 'Sınav',
  ExamSession: 'Oturum',
  Question: 'Soru',
  Event: 'Olay',
  ConsentRecord: 'Rıza kaydı',
  ReferenceFaceTemplate: 'Yüz şablonu',
  ReviewerDecision: 'Karar',
}

const GROUPS: { value: string; label: string }[] = [
  { value: '', label: 'Bütün işlemler' },
  { value: 'user.', label: 'Hesaplar' },
  { value: 'review.', label: 'İnceleme ve karar' },
  { value: 'report.', label: 'Raporlar' },
  { value: 'consent.', label: 'Rızalar' },
  { value: 'session.', label: 'Oturumlar' },
  { value: 'retention.', label: 'Veri saklama' },
]

/** Değer JSON metni; okunur kısa satırlara çevrilir. */
function describe(value: string | null): string[] {
  if (!value) return []
  try {
    const parsed = JSON.parse(value) as unknown
    if (parsed && typeof parsed === 'object') return Object.entries(parsed as Record<string, unknown>).map(([key, item]) => `${key}: ${typeof item === 'object' ? JSON.stringify(item) : String(item)}`)
    return [String(parsed)]
  } catch {
    return [value]
  }
}

/**
 * Denetim kaydı (Stitch 32). Kayıt değiştirilemez (K-10, K-32); burada yalnızca okunur. Değişiklikler "eski → yeni"
 * olarak, eski değer üstü çizili gösterilir.
 */
export function AuditLogPage() {
  const [action, setAction] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)

  const load = useCallback(
    (signal: AbortSignal) =>
      listAuditLogs(
        {
          action: action || undefined,
          from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
          to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [action, from, to, page],
  )
  const { data, error, loading, reload } = useResource(load, 'Denetim kaydı yüklenemedi.')
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  const filter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }]}
        title="Denetim kaydı"
        description="Kim, neyi, ne zaman yaptı."
        info={
          <ul>
            <li>Platformdaki önemli işlemler kaydedilir: girişler, rol değişiklikleri, rızalar, kararlar, rapor indirme.</li>
            <li>Kayıt değiştirilemez ve silinemez.</li>
            <li>Bir satıra tıklayınca değişikliğin önceki ve sonraki değeri açılır.</li>
          </ul>
        }
      />

      <div className="admin-toolbar">
        <label className="field field-inline">
          <span className="field-label">İşlem</span>
          <select className="select" value={action} onChange={(event) => filter(() => setAction(event.target.value))}>
            {GROUPS.map((group) => (
              <option key={group.value} value={group.value}>
                {group.label}
              </option>
            ))}
          </select>
        </label>
        <label className="field field-inline">
          <span className="field-label">Başlangıç</span>
          <input className="select" type="date" value={from} onChange={(event) => filter(() => setFrom(event.target.value))} />
        </label>
        <label className="field field-inline">
          <span className="field-label">Bitiş</span>
          <input className="select" type="date" value={to} onChange={(event) => filter(() => setTo(event.target.value))} />
        </label>
        {data ? <p className="admin-row-sub">{data.total} kayıt</p> : null}
      </div>

      {error ? (
        <Alert tone="danger" title="Denetim kaydı yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <Skeleton height="24rem" radius="lg" />
      ) : data && data.items.length === 0 ? (
        <EmptyState icon={ScrollText} title="Kayıt yok">
          Seçtiğiniz filtreye uyan işlem bulunamadı.
        </EmptyState>
      ) : data ? (
        <>
          <ol className="audit-list">
            {data.items.map((item) => (
              <AuditRow key={item.id} item={item} open={open === item.id} onToggle={() => setOpen(open === item.id ? null : item.id)} />
            ))}
          </ol>
          <div className="pager">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Önceki
            </Button>
            <span className="admin-row-sub">
              Sayfa {page} / {pages}
            </span>
            <Button variant="secondary" disabled={page >= pages} onClick={() => setPage(page + 1)}>
              Sonraki
            </Button>
          </div>
        </>
      ) : null}
    </div>
  )
}

function AuditRow({ item, open, onToggle }: { item: AuditLogView; open: boolean; onToggle: () => void }) {
  const before = describe(item.oldValue)
  const after = describe(item.newValue)
  const hasDetail = before.length > 0 || after.length > 0
  return (
    <li className={['audit-row', open && 'is-open'].filter(Boolean).join(' ')}>
      <button type="button" className="audit-summary" aria-expanded={hasDetail ? open : undefined} disabled={!hasDetail} onClick={onToggle}>
        <time className="audit-time numeric" dateTime={item.occurredAt}>
          {formatDateTime(item.occurredAt)}
        </time>
        <span className="audit-actor">{item.actorName ?? 'Sistem'}</span>
        <span className="audit-action">{ACTION_LABEL[item.action] ?? item.action}</span>
        <span className="audit-target">{item.targetType ? (TARGET_LABEL[item.targetType] ?? item.targetType) : ''}</span>
        {hasDetail ? <ChevronDown size={18} className="audit-chevron" aria-hidden="true" /> : <span />}
      </button>
      <AnimatePresence initial={false}>
        {open && hasDetail ? (
          <motion.div className="audit-detail" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="audit-diff">
              {before.length ? (
                <div>
                  <p className="kicker">Önceki</p>
                  {before.map((line) => (
                    <del key={line}>{line}</del>
                  ))}
                </div>
              ) : null}
              {after.length ? (
                <div>
                  <p className="kicker">Sonraki</p>
                  {after.map((line) => (
                    <ins key={line}>{line}</ins>
                  ))}
                </div>
              ) : null}
            </div>
            <p className="admin-row-sub">İşlem kodu: <code>{item.action}</code></p>
          </motion.div>
        ) : null}
      </AnimatePresence>
    </li>
  )
}
