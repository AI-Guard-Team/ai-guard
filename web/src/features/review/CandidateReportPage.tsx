import { ArrowLeft, Download, Printer } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../../api/client'
import { EXAM_TYPE_LABEL, type ExamType } from '../../api/exams'
import { downloadCandidateReport, getCandidateReport, type ReportFormat } from '../../api/review'
import { useAuth } from '../../auth/useAuth'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Seal } from '../../components/Seal'
import { Skeleton } from '../../components/Skeleton'
import { useResource } from '../../hooks/useResource'
import { DECISION_LABEL, DECISION_TONE, FLAG_LABEL, STATUS_LABEL, eventLabel } from '../../lib/eventCatalog'
import { docNumber, formatDate, formatDateTime, formatNumber, formatTimeRange } from '../../lib/format'
import { common } from '../../i18n/common'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import './report.css'

const messages = defineMessages({
  tr: {
    failed: 'Rapor yüklenemedi.',
    downloadFailed: 'Rapor indirilemedi.',
    backToFile: 'Dosyaya dön',
    reports: 'Raporlar',
    print: 'Yazdır',
    pdf: 'PDF indir',
    openFailed: 'Rapor açılamadı',
    report: 'Aday raporu',
    noDecision: 'Karar yok',
    notDecided: 'Karar verilmedi',
    exam: 'Sınav',
    type: 'Tür',
    date: 'Tarih',
    duration: 'Süre',
    status: 'Durum',
    attempts: 'Kimlik doğrulama denemesi',
    result: 'Sınav sonucu',
    correct: 'Doğru',
    wrong: 'Yanlış',
    blank: 'Boş',
    questions: 'Soru',
    unmonitored: 'İzlenmeyen sürede',
    noResult: 'Sınav tamamlanmadığı için sonuç yok.',
    risk: 'Risk değerlendirmesi',
    above: 'Eşik üstü',
    below: 'Eşik altı',
    flags: (list: string) => `İşaretler: ${list}`,
    deleted: (date: string) => `İzleme verileri ${date} tarihinde saklama süresi dolduğu için silindi.`,
    event: 'Olay',
    count: 'Adet',
    falseAlarm: 'Yanlış alarm',
    contribution: 'Katkı',
    noEvents: 'Olay kaydı yok.',
    decisionNotes: 'Karar ve notlar',
    noReviewerDecision: 'Değerlendirici kararı yok.',
    disclaimer: 'Tespit edilen olaylar kopya kanıtı değildir; karar değerlendiriciye aittir.',
  },
  en: {
    failed: 'The report could not be loaded.',
    downloadFailed: 'The report could not be downloaded.',
    backToFile: 'Back to file',
    reports: 'Reports',
    print: 'Print',
    pdf: 'Download PDF',
    openFailed: 'The report could not be opened',
    report: 'Candidate report',
    noDecision: 'No decision',
    notDecided: 'No decision made',
    exam: 'Exam',
    type: 'Type',
    date: 'Date',
    duration: 'Duration',
    status: 'Status',
    attempts: 'Identity verification attempts',
    result: 'Exam result',
    correct: 'Correct',
    wrong: 'Wrong',
    blank: 'Blank',
    questions: 'Questions',
    unmonitored: 'While unmonitored',
    noResult: 'There is no result because the exam was not completed.',
    risk: 'Risk assessment',
    above: 'Above threshold',
    below: 'Below threshold',
    flags: (list: string) => `Flags: ${list}`,
    deleted: (date: string) => `Monitoring data was deleted on ${date} because the retention period ended.`,
    event: 'Event',
    count: 'Count',
    falseAlarm: 'False alarms',
    contribution: 'Contribution',
    noEvents: 'No events recorded.',
    decisionNotes: 'Decision and notes',
    noReviewerDecision: 'No reviewer decision.',
    disclaimer: 'Detected events are not proof of cheating; the decision belongs to the reviewer.',
  },
})

/**
 * Aday raporu, resmî A4 belge (Stitch 22; `GET /api/reports/...`, K-32). Ekranda belge, yazdırılınca da aynı belge;
 * PDF ve Excel sunucunun dışa aktarması. Değerlendirici ve yönetici açabilir (rapor görüntüleme denetime yazılıyor).
 */
export function CandidateReportPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { examId = '', sessionId = '' } = useParams()
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => getCandidateReport(examId, sessionId, signal), [examId, sessionId])
  const { data: report, error, reload } = useResource(load, m.failed)
  const [downloading, setDownloading] = useState<ReportFormat | null>(null)
  const [downloadError, setDownloadError] = useState<string | null>(null)

  const download = async (format: ReportFormat) => {
    if (downloading) return
    setDownloading(format)
    setDownloadError(null)
    try {
      await downloadCandidateReport(examId, sessionId, format)
    } catch (caught) {
      setDownloadError(caught instanceof ApiError ? caught.message : m.downloadFailed)
    } finally {
      setDownloading(null)
    }
  }

  const back = user?.role === 'Reviewer' ? `/review/sessions/${sessionId}` : '/admin/reports'

  return (
    <div className="report-page">
      <div className="report-toolbar">
        <Link className="review-back" to={back}>
          <ArrowLeft size={16} aria-hidden="true" />
          {user?.role === 'Reviewer' ? m.backToFile : m.reports}
        </Link>
        <div className="report-actions">
          <Button variant="secondary" icon={<Printer size={18} aria-hidden="true" />} onClick={() => window.print()} disabled={!report}>
            {m.print}
          </Button>
          <Button variant="secondary" loading={downloading === 'Xlsx'} icon={<Download size={18} aria-hidden="true" />} onClick={() => void download('Xlsx')} disabled={!report}>
            Excel
          </Button>
          <Button loading={downloading === 'Pdf'} icon={<Download size={18} aria-hidden="true" />} onClick={() => void download('Pdf')} disabled={!report}>
            {m.pdf}
          </Button>
        </div>
      </div>

      {downloadError ? <Alert tone="danger">{downloadError}</Alert> : null}
      {error && !report ? (
        <Alert tone="danger" title={m.openFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {!report ? (
        !error ? <Skeleton className="report-sheet" height="60rem" radius="lg" /> : null
      ) : (
        <article className="report-sheet" aria-label={m.report}>
          <header className="report-head">
            <div>
              <p className="kicker">
                <span className="doc-card-no">{docNumber(report.sessionId, report.startedAt ?? report.createdAt)}</span> · {m.report}
              </p>
              <h1>{report.candidateName}</h1>
              <p className="report-sub">{report.candidateEmail}</p>
            </div>
            {report.decision ? (
              <Seal
                label={DECISION_LABEL[report.decision.decision]}
                ring={report.decision.reviewerName.toLocaleUpperCase(getLocale())}
                tone={DECISION_TONE[report.decision.decision]}
                size={116}
                tilt={-8}
              />
            ) : (
              <Seal label={m.noDecision} state="empty" size={104} title={m.notDecided} />
            )}
          </header>

          <hr className="perforation" />

          <dl className="report-facts">
            <div>
              <dt>{m.exam}</dt>
              <dd>{report.examName}</dd>
            </div>
            <div>
              <dt>{m.type}</dt>
              <dd>{EXAM_TYPE_LABEL[report.examType as ExamType] ?? report.examType}</dd>
            </div>
            <div>
              <dt>{m.date}</dt>
              <dd className="numeric">
                {formatDate(report.startedAt ?? report.createdAt)} · {formatTimeRange(report.startedAt ?? report.createdAt, report.endedAt)}
              </dd>
            </div>
            <div>
              <dt>{m.duration}</dt>
              <dd className="numeric">{c.minutes(report.durationMinutes)}</dd>
            </div>
            <div>
              <dt>{m.status}</dt>
              <dd>{STATUS_LABEL[report.status]}</dd>
            </div>
            <div>
              <dt>{m.attempts}</dt>
              <dd className="numeric">{report.verificationAttempts}</dd>
            </div>
          </dl>

          <section className="report-section">
            <h2>{m.result}</h2>
            {report.result ? (
              <dl className="report-result">
                <div>
                  <dt>{m.correct}</dt>
                  <dd className="numeric">{report.result.correct}</dd>
                </div>
                <div>
                  <dt>{m.wrong}</dt>
                  <dd className="numeric">{report.result.wrong}</dd>
                </div>
                <div>
                  <dt>{m.blank}</dt>
                  <dd className="numeric">{report.result.blank}</dd>
                </div>
                <div>
                  <dt>{m.questions}</dt>
                  <dd className="numeric">{report.result.questionCount}</dd>
                </div>
                <div>
                  <dt>{m.unmonitored}</dt>
                  <dd className="numeric">{report.result.unmonitored}</dd>
                </div>
              </dl>
            ) : (
              <p className="report-muted">{m.noResult}</p>
            )}
          </section>

          <section className="report-section">
            <h2>{m.risk}</h2>
            <p className="report-score">
              <span className="numeric">{formatNumber(report.riskScore)}</span>
              <span className="kicker">{report.flags.includes('Risky') ? m.above : m.below}</span>
            </p>
            {report.flags.filter((flag) => flag !== 'Risky').length ? (
              <p className="report-flags">{m.flags(report.flags.filter((flag) => flag !== 'Risky').map((flag) => FLAG_LABEL[flag]).join(', '))}</p>
            ) : null}
            {report.topReasons.length ? (
              <ol className="report-reasons">
                {report.topReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ol>
            ) : null}
            {report.monitoringDataDeletedAt ? (
              <p className="report-muted">{m.deleted(formatDate(report.monitoringDataDeletedAt))}</p>
            ) : report.events.length ? (
              <table className="report-table">
                <thead>
                  <tr>
                    <th scope="col">{m.event}</th>
                    <th scope="col">{m.count}</th>
                    <th scope="col">{m.falseAlarm}</th>
                    <th scope="col">{m.contribution}</th>
                  </tr>
                </thead>
                <tbody>
                  {report.events.map((row) => (
                    <tr key={row.type}>
                      <td>{eventLabel(row.type)}</td>
                      <td className="numeric">{row.count}</td>
                      <td className="numeric">{row.falseAlarms}</td>
                      <td className="numeric">{formatNumber(row.contribution, 1)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="report-muted">{m.noEvents}</p>
            )}
          </section>

          <section className="report-section">
            <h2>{m.decisionNotes}</h2>
            {report.decision ? (
              <p>
                <strong>{DECISION_LABEL[report.decision.decision]}</strong> · {report.decision.reviewerName} · {formatDateTime(report.decision.decidedAt)}
                {report.decision.note ? <span className="report-quote">{report.decision.note}</span> : null}
              </p>
            ) : (
              <p className="report-muted">{m.noReviewerDecision}</p>
            )}
            {report.notes.length ? (
              <ol className="report-notes">
                {report.notes.map((note) => (
                  <li key={note.id}>
                    <span className="kicker">
                      {note.authorName} · {formatDateTime(note.createdAt)}
                    </span>
                    <p>{note.text}</p>
                  </li>
                ))}
              </ol>
            ) : null}
          </section>

          <footer className="report-foot">
            <p>{m.disclaimer}</p>
          </footer>
        </article>
      )}
    </div>
  )
}
