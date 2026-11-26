import { Download } from 'lucide-react'
import { useCallback, useState } from 'react'
import { ApiError } from '../../api/client'
import { getExamSummary, type ExamSummaryReport } from '../../api/management'
import { downloadExamCsv, type ReviewDecisionType } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import { useToast } from '../../components/useToast'
import { useCountUp } from '../../hooks/useCountUp'
import { useResource } from '../../hooks/useResource'
import { defineMessages, useMessages } from '../../i18n/messages'
import { DECISION_LABEL, DECISION_TONE, eventLabel } from '../../lib/eventCatalog'
import { formatNumber } from '../../lib/format'

const DECISIONS: ReviewDecisionType[] = ['Clean', 'Suspicious', 'Violation']

const messages = defineMessages({
  tr: {
    failed: 'Sınav özeti yüklenemedi.',
    title: 'Sınav özeti',
    csv: 'Bütün oturumlar (CSV)',
    csvFailed: 'CSV indirilemedi.',
    assigned: 'Atanan aday',
    started: (n: number) => `${n} başladı`,
    completed: 'Tamamlanan',
    inProgress: (n: number) => `${n} sürüyor`,
    awaiting: 'Karar bekleyen',
    decided: (n: number) => `${n} karara bağlandı`,
    risky: 'Riskli oturum',
    avgRisk: (avg: string, max: string) => `Ort. skor ${avg} · en yüksek ${max}`,
    decisions: 'Değerlendirici kararları',
    noDecisions: 'Henüz karar yok.',
    events: 'Olay türleri',
    noEvents: 'Bu sınavda olay kaydı yok.',
    falseAlarms: (n: number) => `${n} yanlış alarm`,
    density: 'Dakika başına olay',
    densitySub: 'Sınav başından itibaren; yanlış alarmlar hariç.',
    minute: (n: number) => `${n}. dakika`,
    eventsAt: (n: number, minute: number) => `${minute}. dakika: ${n} olay`,
    tableView: 'Tablo olarak göster',
    count: 'Olay',
    minuteHeader: 'Dakika',
    correct: (avg: string, total: number) => `Ortalama doğru ${avg}/${total}`,
  },
  en: {
    failed: 'The exam summary could not be loaded.',
    title: 'Exam summary',
    csv: 'All sessions (CSV)',
    csvFailed: 'The CSV could not be downloaded.',
    assigned: 'Assigned candidates',
    started: (n: number) => `${n} started`,
    completed: 'Completed',
    inProgress: (n: number) => `${n} in progress`,
    awaiting: 'Awaiting decision',
    decided: (n: number) => `${n} decided`,
    risky: 'Risky sessions',
    avgRisk: (avg: string, max: string) => `Avg. score ${avg} · highest ${max}`,
    decisions: 'Reviewer decisions',
    noDecisions: 'No decisions yet.',
    events: 'Event types',
    noEvents: 'No events were recorded in this exam.',
    falseAlarms: (n: number) => `${n} false ${n === 1 ? 'alarm' : 'alarms'}`,
    density: 'Events per minute',
    densitySub: 'From the start of the exam; false alarms excluded.',
    minute: (n: number) => `Minute ${n}`,
    eventsAt: (n: number, minute: number) => `Minute ${minute}: ${n} ${n === 1 ? 'event' : 'events'}`,
    tableView: 'Show as a table',
    count: 'Events',
    minuteHeader: 'Minute',
    correct: (avg: string, total: number) => `Average correct ${avg}/${total}`,
  },
})

/** Sayı karosu: değer sayarak gelir (hareket azaltılmışsa doğrudan). */
function Tile({ label, value, sub, tone }: { label: string; value: number; sub: string; tone?: 'warning' | 'danger' }) {
  const shown = useCountUp(value)
  return (
    <div className={['summary-tile', tone && `is-${tone}`].filter(Boolean).join(' ')}>
      <span className="summary-tile-label">{label}</span>
      <strong className="summary-tile-value numeric">{formatNumber(shown)}</strong>
      <span className="admin-row-sub">{sub}</span>
    </div>
  )
}

/**
 * Sınav düzeyi toplu rapor (Stitch 30; `GET /api/reports/exams/{id}`, K-49). Grafikler tek seri: ana renk
 * büyüklük, karar dağılımı ayrılmış durum renkleri + etiket. Her grafiğin tablo görünümü var.
 */
export function ExamSummaryPanel({ examId }: { examId: string }) {
  const m = useMessages(messages)
  const showToast = useToast()
  const load = useCallback((signal: AbortSignal) => getExamSummary(examId, signal), [examId])
  const { data, error } = useResource(load, m.failed)
  const [downloading, setDownloading] = useState(false)

  const csv = async () => {
    setDownloading(true)
    try {
      await downloadExamCsv(examId)
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.csvFailed, 'error')
    } finally {
      setDownloading(false)
    }
  }

  if (error && !data) return <Alert tone="danger">{error}</Alert>
  if (!data || data.examId !== examId) return <Skeleton height="18rem" radius="lg" />

  return (
    <section className="summary" aria-labelledby="exam-summary-title">
      <div className="summary-head">
        <h2 className="editor-card-title" id="exam-summary-title">
          {m.title}
        </h2>
        <Button variant="secondary" icon={<Download size={16} aria-hidden="true" />} loading={downloading} onClick={() => void csv()}>
          {m.csv}
        </Button>
      </div>

      <div className="summary-tiles">
        <Tile label={m.assigned} value={data.assigned} sub={m.started(data.started)} />
        <Tile label={m.completed} value={data.completed} sub={m.inProgress(data.inProgress)} />
        <Tile label={m.awaiting} value={data.awaitingReview} sub={m.decided(data.decided)} tone={data.awaitingReview > 0 ? 'warning' : undefined} />
        <Tile
          label={m.risky}
          value={data.risky}
          sub={m.avgRisk(formatNumber(data.averageRisk, 1), formatNumber(data.maxRisk, 1))}
          tone={data.risky > 0 ? 'danger' : undefined}
        />
      </div>

      <div className="summary-charts">
        <div className="editor-card">
          <h3 className="summary-chart-title">{m.decisions}</h3>
          <DecisionBar data={data} />
          {data.questionCount > 0 && data.completed > 0 ? (
            <p className="admin-row-sub">{m.correct(formatNumber(data.averageCorrect, 1), data.questionCount)}</p>
          ) : null}
        </div>

        <div className="editor-card">
          <h3 className="summary-chart-title">{m.events}</h3>
          <EventBars data={data} />
        </div>

        <div className="editor-card summary-wide">
          <h3 className="summary-chart-title">{m.density}</h3>
          <p className="editor-card-sub">{m.densitySub}</p>
          <DensityChart data={data} />
        </div>
      </div>
    </section>
  )
}

function DecisionBar({ data }: { data: ExamSummaryReport }) {
  const m = useMessages(messages)
  const counts: Record<ReviewDecisionType, number> = { Clean: data.decisions.clean, Suspicious: data.decisions.suspicious, Violation: data.decisions.violation }
  const total = DECISIONS.reduce((sum, key) => sum + counts[key], 0)
  if (total === 0) return <p className="admin-row-sub">{m.noDecisions}</p>

  return (
    <>
      <div className="decision-bar" role="img" aria-label={DECISIONS.map((key) => `${DECISION_LABEL[key]} ${counts[key]}`).join(', ')}>
        {DECISIONS.filter((key) => counts[key] > 0).map((key) => (
          <span key={key} className={`decision-seg tone-${DECISION_TONE[key]}`} style={{ flexGrow: counts[key] }} title={`${DECISION_LABEL[key]}: ${counts[key]}`} />
        ))}
      </div>
      <ul className="decision-legend">
        {DECISIONS.map((key) => (
          <li key={key}>
            <span className={`decision-dot tone-${DECISION_TONE[key]}`} aria-hidden="true" />
            {DECISION_LABEL[key]}
            <strong className="numeric">{counts[key]}</strong>
          </li>
        ))}
      </ul>
    </>
  )
}

function EventBars({ data }: { data: ExamSummaryReport }) {
  const m = useMessages(messages)
  const rows = [...data.events].filter((row) => row.count > 0).sort((a, b) => b.count - a.count)
  if (rows.length === 0) return <p className="admin-row-sub">{m.noEvents}</p>
  const max = Math.max(...rows.map((row) => row.count))

  return (
    <ul className="hbar-list">
      {rows.map((row) => (
        <li key={row.type} className="hbar-row" title={row.falseAlarms ? m.falseAlarms(row.falseAlarms) : undefined}>
          <span className="hbar-label">{eventLabel(row.type)}</span>
          <span className="hbar-track" aria-hidden="true">
            <span className="hbar-fill" style={{ width: `${(row.count / max) * 100}%` }} />
          </span>
          <span className="hbar-value numeric">{row.count}</span>
        </li>
      ))}
    </ul>
  )
}

function DensityChart({ data }: { data: ExamSummaryReport }) {
  const m = useMessages(messages)
  const [hover, setHover] = useState<number | null>(null)
  const max = Math.max(1, ...data.density.map((d) => d.events))
  const total = data.density.reduce((sum, d) => sum + d.events, 0)
  if (total === 0) return <p className="admin-row-sub">{m.noEvents}</p>
  const active = hover !== null ? data.density[hover] : null
  // Eksende yalnızca birkaç dakika etiketi: başlangıç, çeyrekler ve son.
  const ticks = new Set([1, ...[0.25, 0.5, 0.75].map((f) => Math.max(1, Math.round(data.density.length * f))), data.density.length])

  return (
    <>
      <div className="density" onMouseLeave={() => setHover(null)}>
        <div className="density-plot" role="img" aria-label={m.density}>
          {data.density.map((d, index) => (
            <span
              key={d.minute}
              className={['density-col', hover === index && 'is-active'].filter(Boolean).join(' ')}
              onMouseEnter={() => setHover(index)}
            >
              <span className="density-bar" style={{ height: d.events ? `${Math.max(4, (d.events / max) * 100)}%` : 0 }} />
            </span>
          ))}
        </div>
        <div className="density-axis" aria-hidden="true">
          {data.density.map((d) => (
            <span key={d.minute}>{ticks.has(d.minute) ? d.minute : ''}</span>
          ))}
        </div>
        <p className="density-readout admin-row-sub" aria-live="polite">
          {active ? m.eventsAt(active.events, active.minute) : ' '}
        </p>
      </div>
      <details className="summary-table">
        <summary>{m.tableView}</summary>
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">{m.minuteHeader}</th>
              <th scope="col">{m.count}</th>
            </tr>
          </thead>
          <tbody>
            {data.density
              .filter((d) => d.events > 0)
              .map((d) => (
                <tr key={d.minute}>
                  <td>{m.minute(d.minute)}</td>
                  <td className="numeric">{d.events}</td>
                </tr>
              ))}
          </tbody>
        </table>
      </details>
    </>
  )
}
