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
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { ACTION_LABEL } from './auditLabels'
import './admin.css'

const PAGE_SIZE = 25

const GROUP_VALUES = ['', 'user.', 'review.', 'report.', 'consent.', 'session.', 'retention.'] as const

const messages = defineMessages({
  tr: {
    targets: {
      User: 'Kullanıcı',
      Exam: 'Sınav',
      ExamSession: 'Oturum',
      Question: 'Soru',
      Event: 'Olay',
      ConsentRecord: 'Rıza kaydı',
      ReferenceFaceTemplate: 'Yüz şablonu',
      ReviewerDecision: 'Karar',
    } as Record<string, string>,
    groups: {
      '': 'Bütün işlemler',
      'user.': 'Hesaplar',
      'review.': 'İnceleme ve karar',
      'report.': 'Raporlar',
      'consent.': 'Rızalar',
      'session.': 'Oturumlar',
      'retention.': 'Veri saklama',
    } as Record<(typeof GROUP_VALUES)[number], string>,
    failed: 'Denetim kaydı yüklenemedi.',
    admin: 'Yönetim',
    title: 'Denetim kaydı',
    description: 'Kim, neyi, ne zaman yaptı.',
    info: [
      'Platformdaki önemli işlemler kaydedilir: girişler, rol değişiklikleri, rızalar, kararlar, rapor indirme.',
      'Kayıt değiştirilemez ve silinemez.',
      'Bir satıra tıklayınca değişikliğin önceki ve sonraki değeri açılır.',
    ],
    action: 'İşlem',
    from: 'Başlangıç',
    to: 'Bitiş',
    rangeError: 'Başlangıç tarihi bitişten sonra olamaz.',
    records: (n: number) => `${n} kayıt`,
    loadFailed: 'Denetim kaydı yüklenemedi',
    none: 'Kayıt yok',
    noneText: 'Seçtiğiniz filtreye uyan işlem bulunamadı.',
    previous: 'Önceki',
    next: 'Sonraki',
    page: (page: number, pages: number) => `Sayfa ${page} / ${pages}`,
    system: 'Sistem',
    before: 'Önceki',
    after: 'Sonraki',
    code: 'İşlem kodu:',
  },
  en: {
    targets: {
      User: 'User',
      Exam: 'Exam',
      ExamSession: 'Session',
      Question: 'Question',
      Event: 'Event',
      ConsentRecord: 'Consent record',
      ReferenceFaceTemplate: 'Face template',
      ReviewerDecision: 'Decision',
    },
    groups: {
      '': 'All actions',
      'user.': 'Accounts',
      'review.': 'Review and decision',
      'report.': 'Reports',
      'consent.': 'Consents',
      'session.': 'Sessions',
      'retention.': 'Data retention',
    },
    failed: 'The audit log could not be loaded.',
    admin: 'Administration',
    title: 'Audit log',
    description: 'Who did what, and when.',
    info: [
      'Important actions on the platform are recorded: sign-ins, role changes, consents, decisions and report downloads.',
      'Records cannot be changed or deleted.',
      'Click a row to see the value before and after the change.',
    ],
    action: 'Action',
    from: 'From',
    to: 'To',
    rangeError: 'The start date cannot be after the end date.',
    records: (n: number) => `${n} ${n === 1 ? 'record' : 'records'}`,
    loadFailed: 'The audit log could not be loaded',
    none: 'No records',
    noneText: 'No actions match the selected filter.',
    previous: 'Previous',
    next: 'Next',
    page: (page: number, pages: number) => `Page ${page} of ${pages}`,
    system: 'System',
    before: 'Before',
    after: 'After',
    code: 'Action code:',
  },
})

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
  const m = useMessages(messages)
  const c = useMessages(common)
  const [action, setAction] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)

  // Ters aralık sunucuya gitmez; kullanıcı hatayı alanın yanında görür.
  const rangeInvalid = Boolean(from && to && from > to)
  const load = useCallback(
    (signal: AbortSignal) =>
      rangeInvalid
        ? Promise.resolve(null)
        : listAuditLogs(
        {
          action: action || undefined,
          from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
          to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
          page,
          pageSize: PAGE_SIZE,
        },
        signal,
      ),
    [action, from, to, page, rangeInvalid],
  )
  const { data, error, loading, reload } = useResource(load, m.failed)
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  const filter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: m.admin }]}
        title={m.title}
        description={m.description}
        info={
          <ul>
            {m.info.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        }
      />

      <div className="admin-toolbar">
        <label className="field field-inline">
          <span className="field-label">{m.action}</span>
          <select className="select" value={action} onChange={(event) => filter(() => setAction(event.target.value))}>
            {GROUP_VALUES.map((value) => (
              <option key={value} value={value}>
                {m.groups[value]}
              </option>
            ))}
          </select>
        </label>
        <label className="field field-inline">
          <span className="field-label">{m.from}</span>
          <input className="select" type="date" value={from} max={to || undefined} aria-invalid={rangeInvalid || undefined} onChange={(event) => filter(() => setFrom(event.target.value))} />
        </label>
        <label className="field field-inline">
          <span className="field-label">{m.to}</span>
          <input className="select" type="date" value={to} min={from || undefined} aria-invalid={rangeInvalid || undefined} onChange={(event) => filter(() => setTo(event.target.value))} />
        </label>
        {data ? <p className="admin-row-sub">{m.records(data.total)}</p> : null}
      </div>

      {rangeInvalid ? <Alert tone="warning">{m.rangeError}</Alert> : null}

      {error ? (
        <Alert tone="danger" title={m.loadFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <Skeleton height="24rem" radius="lg" />
      ) : data && data.items.length === 0 ? (
        <EmptyState icon={ScrollText} title={m.none}>
          {m.noneText}
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
              {m.previous}
            </Button>
            <span className="admin-row-sub">{m.page(page, pages)}</span>
            <Button variant="secondary" disabled={page >= pages} onClick={() => setPage(page + 1)}>
              {m.next}
            </Button>
          </div>
        </>
      ) : null}
    </div>
  )
}

function AuditRow({ item, open, onToggle }: { item: AuditLogView; open: boolean; onToggle: () => void }) {
  const m = useMessages(messages)
  const before = describe(item.oldValue)
  const after = describe(item.newValue)
  const hasDetail = before.length > 0 || after.length > 0
  return (
    <li className={['audit-row', open && 'is-open'].filter(Boolean).join(' ')}>
      <button type="button" className="audit-summary" aria-expanded={hasDetail ? open : undefined} disabled={!hasDetail} onClick={onToggle}>
        <time className="audit-time numeric" dateTime={item.occurredAt}>
          {formatDateTime(item.occurredAt)}
        </time>
        <span className="audit-actor">{item.actorName ?? m.system}</span>
        <span className="audit-action">{ACTION_LABEL[item.action] ?? item.action}</span>
        <span className="audit-target">{item.targetType ? (m.targets[item.targetType] ?? item.targetType) : ''}</span>
        {hasDetail ? <ChevronDown size={18} className="audit-chevron" aria-hidden="true" /> : <span />}
      </button>
      <AnimatePresence initial={false}>
        {open && hasDetail ? (
          <motion.div className="audit-detail" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="audit-diff">
              {before.length ? (
                <div>
                  <p className="kicker">{m.before}</p>
                  {before.map((line) => (
                    <del key={line}>{line}</del>
                  ))}
                </div>
              ) : null}
              {after.length ? (
                <div>
                  <p className="kicker">{m.after}</p>
                  {after.map((line) => (
                    <ins key={line}>{line}</ins>
                  ))}
                </div>
              ) : null}
            </div>
            <p className="admin-row-sub">
              {m.code} <code>{item.action}</code>
            </p>
          </motion.div>
        ) : null}
      </AnimatePresence>
    </li>
  )
}
