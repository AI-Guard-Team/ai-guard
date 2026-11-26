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
import './report.css'

/**
 * Aday raporu, resmî A4 belge (Stitch 22; `GET /api/reports/...`, K-32). Ekranda belge, yazdırılınca da aynı belge;
 * PDF ve Excel sunucunun dışa aktarması. Değerlendirici ve yönetici açabilir (rapor görüntüleme denetime yazılıyor).
 */
export function CandidateReportPage() {
  const { examId = '', sessionId = '' } = useParams()
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => getCandidateReport(examId, sessionId, signal), [examId, sessionId])
  const { data: report, error, reload } = useResource(load, 'Rapor yüklenemedi.')
  const [downloading, setDownloading] = useState<ReportFormat | null>(null)
  const [downloadError, setDownloadError] = useState<string | null>(null)

  const download = async (format: ReportFormat) => {
    setDownloading(format)
    setDownloadError(null)
    try {
      await downloadCandidateReport(examId, sessionId, format)
    } catch (caught) {
      setDownloadError(caught instanceof ApiError ? caught.message : 'Rapor indirilemedi.')
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
          {user?.role === 'Reviewer' ? 'Dosyaya dön' : 'Raporlar'}
        </Link>
        <div className="report-actions">
          <Button variant="secondary" icon={<Printer size={18} aria-hidden="true" />} onClick={() => window.print()} disabled={!report}>
            Yazdır
          </Button>
          <Button variant="secondary" loading={downloading === 'Xlsx'} icon={<Download size={18} aria-hidden="true" />} onClick={() => void download('Xlsx')} disabled={!report}>
            Excel
          </Button>
          <Button loading={downloading === 'Pdf'} icon={<Download size={18} aria-hidden="true" />} onClick={() => void download('Pdf')} disabled={!report}>
            PDF indir
          </Button>
        </div>
      </div>

      {downloadError ? <Alert tone="danger">{downloadError}</Alert> : null}
      {error && !report ? (
        <Alert tone="danger" title="Rapor açılamadı">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {!report ? (
        !error ? <Skeleton className="report-sheet" height="60rem" radius="lg" /> : null
      ) : (
        <article className="report-sheet" aria-label="Aday raporu">
          <header className="report-head">
            <div>
              <p className="kicker">
                <span className="doc-card-no">{docNumber(report.sessionId, report.startedAt ?? report.createdAt)}</span> · Aday raporu
              </p>
              <h1>{report.candidateName}</h1>
              <p className="report-sub">{report.candidateEmail}</p>
            </div>
            {report.decision ? (
              <Seal
                label={DECISION_LABEL[report.decision.decision]}
                ring={report.decision.reviewerName.toLocaleUpperCase('tr-TR')}
                tone={DECISION_TONE[report.decision.decision]}
                size={116}
                tilt={-8}
              />
            ) : (
              <Seal label="Karar yok" state="empty" size={104} title="Karar verilmedi" />
            )}
          </header>

          <hr className="perforation" />

          <dl className="report-facts">
            <div>
              <dt>Sınav</dt>
              <dd>{report.examName}</dd>
            </div>
            <div>
              <dt>Tür</dt>
              <dd>{EXAM_TYPE_LABEL[report.examType as ExamType] ?? report.examType}</dd>
            </div>
            <div>
              <dt>Tarih</dt>
              <dd className="numeric">
                {formatDate(report.startedAt ?? report.createdAt)} · {formatTimeRange(report.startedAt ?? report.createdAt, report.endedAt)}
              </dd>
            </div>
            <div>
              <dt>Süre</dt>
              <dd className="numeric">{report.durationMinutes} dakika</dd>
            </div>
            <div>
              <dt>Durum</dt>
              <dd>{STATUS_LABEL[report.status]}</dd>
            </div>
            <div>
              <dt>Kimlik doğrulama denemesi</dt>
              <dd className="numeric">{report.verificationAttempts}</dd>
            </div>
          </dl>

          <section className="report-section">
            <h2>Sınav sonucu</h2>
            {report.result ? (
              <dl className="report-result">
                <div>
                  <dt>Doğru</dt>
                  <dd className="numeric">{report.result.correct}</dd>
                </div>
                <div>
                  <dt>Yanlış</dt>
                  <dd className="numeric">{report.result.wrong}</dd>
                </div>
                <div>
                  <dt>Boş</dt>
                  <dd className="numeric">{report.result.blank}</dd>
                </div>
                <div>
                  <dt>Soru</dt>
                  <dd className="numeric">{report.result.questionCount}</dd>
                </div>
                <div>
                  <dt>İzlenmeyen sürede</dt>
                  <dd className="numeric">{report.result.unmonitored}</dd>
                </div>
              </dl>
            ) : (
              <p className="report-muted">Sınav tamamlanmadığı için sonuç yok.</p>
            )}
          </section>

          <section className="report-section">
            <h2>Risk değerlendirmesi</h2>
            <p className="report-score">
              <span className="numeric">{formatNumber(report.riskScore)}</span>
              <span className="kicker">{report.flags.includes('Risky') ? 'Eşik üstü' : 'Eşik altı'}</span>
            </p>
            {report.flags.filter((flag) => flag !== 'Risky').length ? (
              <p className="report-flags">İşaretler: {report.flags.filter((flag) => flag !== 'Risky').map((flag) => FLAG_LABEL[flag]).join(', ')}</p>
            ) : null}
            {report.topReasons.length ? (
              <ol className="report-reasons">
                {report.topReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ol>
            ) : null}
            {report.monitoringDataDeletedAt ? (
              <p className="report-muted">İzleme verileri {formatDate(report.monitoringDataDeletedAt)} tarihinde saklama süresi dolduğu için silindi.</p>
            ) : report.events.length ? (
              <table className="report-table">
                <thead>
                  <tr>
                    <th scope="col">Olay</th>
                    <th scope="col">Adet</th>
                    <th scope="col">Yanlış alarm</th>
                    <th scope="col">Katkı</th>
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
              <p className="report-muted">Olay kaydı yok.</p>
            )}
          </section>

          <section className="report-section">
            <h2>Karar ve notlar</h2>
            {report.decision ? (
              <p>
                <strong>{DECISION_LABEL[report.decision.decision]}</strong> · {report.decision.reviewerName} · {formatDateTime(report.decision.decidedAt)}
                {report.decision.note ? <span className="report-quote">{report.decision.note}</span> : null}
              </p>
            ) : (
              <p className="report-muted">Değerlendirici kararı yok.</p>
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
            <p>Tespit edilen olaylar kopya kanıtı değildir; karar değerlendiriciye aittir.</p>
          </footer>
        </article>
      )}
    </div>
  )
}
