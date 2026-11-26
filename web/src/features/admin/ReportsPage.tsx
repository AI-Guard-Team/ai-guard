import { Download, FileBarChart, FileText } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../../api/client'
import { listExams } from '../../api/exams'
import { downloadCandidateReport, getReviewSessions, type ReportFormat } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { RiskScore } from '../../components/RiskScore'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { DECISION_LABEL, DECISION_TONE, STATUS_LABEL, STATUS_TONE } from '../../lib/eventCatalog'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import './admin.css'

const loadExams = (signal: AbortSignal) => listExams(signal)

const messages = defineMessages({
  tr: {
    examsFailed: 'Sınavlar yüklenemedi.',
    sessionsFailed: 'Oturumlar yüklenemedi.',
    downloadFailed: 'Rapor indirilemedi.',
    admin: 'Yönetim',
    title: 'Raporlar',
    description: 'Her adayın sonucu, risk skoru, olayları ve değerlendirici kararı.',
    info: [
      'Önce sınavı seçin; o sınava giren adayların oturumları listelenir.',
      '"Raporu aç" ekranda belge görünümünü açar; PDF ve Excel aynı raporun dosyasıdır.',
      'Rapor görüntüleme ve indirme denetim kaydına yazılır.',
    ],
    noExams: 'Henüz sınav yok',
    noExamsText: 'Sınav oluşturulup adaylar girdikçe raporlar burada görünür.',
    exam: 'Sınav',
    sessions: (n: number) => `${n} oturum`,
    noSessions: 'Bu sınavda oturum yok',
    noSessionsText: 'Adaylar sınava girdikçe oturumları ve raporları burada listelenir.',
    candidate: 'Aday',
    started: 'Başlangıç',
    status: 'Durum',
    risk: 'Risk',
    decision: 'Karar',
    report: 'Rapor',
    open: 'Raporu aç',
    pdf: (name: string) => `${name} raporunu PDF olarak indir`,
    xlsx: (name: string) => `${name} raporunu Excel olarak indir`,
  },
  en: {
    examsFailed: 'Exams could not be loaded.',
    sessionsFailed: 'Sessions could not be loaded.',
    downloadFailed: 'The report could not be downloaded.',
    admin: 'Administration',
    title: 'Reports',
    description: 'Each candidate’s result, risk score, events and reviewer decision.',
    info: [
      'Choose an exam first; the sessions of its candidates are listed.',
      '"Open report" shows the document view; PDF and Excel are files of the same report.',
      'Viewing and downloading reports is written to the audit log.',
    ],
    noExams: 'No exams yet',
    noExamsText: 'Reports appear here as exams are created and candidates take them.',
    exam: 'Exam',
    sessions: (n: number) => `${n} ${n === 1 ? 'session' : 'sessions'}`,
    noSessions: 'No sessions in this exam',
    noSessionsText: 'Sessions and reports are listed here as candidates take the exam.',
    candidate: 'Candidate',
    started: 'Started',
    status: 'Status',
    risk: 'Risk',
    decision: 'Decision',
    report: 'Report',
    open: 'Open report',
    pdf: (name: string) => `Download ${name}’s report as PDF`,
    xlsx: (name: string) => `Download ${name}’s report as Excel`,
  },
})

/**
 * Raporlar (Stitch 30): sınav seçilir, o sınavın oturumları listelenir; her aday için rapor açılır ya da PDF/Excel
 * indirilir. Yönetici raporları yalnızca okur (K-31); rapor görüntüleme ve indirme denetim kaydına yazılır.
 */
export function ReportsPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const exams = useResource(loadExams, m.examsFailed)
  const [examId, setExamId] = useState<string>('')
  const selected = examId || exams.data?.[0]?.id || ''

  const loadSessions = useCallback((signal: AbortSignal) => (selected ? getReviewSessions(selected, { sort: 'StartedAt' }, signal) : Promise.resolve(null)), [selected])
  const sessions = useResource(loadSessions, m.sessionsFailed)
  const showToast = useToast()
  const [downloading, setDownloading] = useState<string | null>(null)

  const download = async (sessionId: string, format: ReportFormat) => {
    if (downloading) return
    setDownloading(`${sessionId}-${format}`)
    try {
      await downloadCandidateReport(selected, sessionId, format)
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.downloadFailed, 'error')
    } finally {
      setDownloading(null)
    }
  }

  const rows = sessions.data?.sessions ?? []

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

      {exams.error && !exams.data ? <Alert tone="danger">{exams.error}</Alert> : null}

      {exams.loading && !exams.data ? (
        <Skeleton height="16rem" radius="lg" />
      ) : exams.data && exams.data.length === 0 ? (
        <EmptyState icon={FileBarChart} title={m.noExams}>
          {m.noExamsText}
        </EmptyState>
      ) : (
        <>
          <div className="admin-toolbar">
            <label className="field field-inline">
              <span className="field-label">{m.exam}</span>
              <select className="select" value={selected} onChange={(event) => setExamId(event.target.value)}>
                {exams.data?.map((exam) => (
                  <option key={exam.id} value={exam.id}>
                    {exam.name}
                  </option>
                ))}
              </select>
            </label>
            {sessions.data ? <p className="admin-row-sub">{m.sessions(rows.length)}</p> : null}
          </div>

          {sessions.error ? <Alert tone="danger">{sessions.error}</Alert> : null}

          {sessions.loading && !sessions.data ? (
            <Skeleton height="14rem" radius="lg" />
          ) : rows.length === 0 ? (
            <EmptyState icon={FileText} title={m.noSessions}>
              {m.noSessionsText}
            </EmptyState>
          ) : (
            <div className="table-card">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">{m.candidate}</th>
                    <th scope="col">{m.started}</th>
                    <th scope="col">{m.status}</th>
                    <th scope="col">{m.risk}</th>
                    <th scope="col">{m.decision}</th>
                    <th scope="col">
                      <span className="visually-hidden">{m.report}</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.sessionId}>
                      <td>
                        <strong>{row.candidateName}</strong>
                      </td>
                      <td className="numeric">{formatDateTime(row.startedAt)}</td>
                      <td>
                        <StatusBadge tone={STATUS_TONE[row.status]} dot>
                          {STATUS_LABEL[row.status]}
                        </StatusBadge>
                      </td>
                      <td>
                        <RiskScore score={row.riskScore} risky={row.flags.includes('Risky')} size="sm" />
                      </td>
                      <td>{row.decision ? <StatusBadge tone={DECISION_TONE[row.decision]}>{DECISION_LABEL[row.decision]}</StatusBadge> : <span className="admin-row-sub">{c.none}</span>}</td>
                      <td className="cell-actions">
                        <Link className="button button-secondary button-md" to={`/reports/exams/${selected}/sessions/${row.sessionId}`}>
                          <FileText size={16} aria-hidden="true" />
                          {m.open}
                        </Link>
                        <Button
                          variant="ghost"
                          aria-label={m.pdf(row.candidateName)}
                          loading={downloading === `${row.sessionId}-Pdf`}
                          icon={<Download size={16} aria-hidden="true" />}
                          onClick={() => void download(row.sessionId, 'Pdf')}
                        >
                          PDF
                        </Button>
                        <Button
                          variant="ghost"
                          aria-label={m.xlsx(row.candidateName)}
                          loading={downloading === `${row.sessionId}-Xlsx`}
                          icon={<Download size={16} aria-hidden="true" />}
                          onClick={() => void download(row.sessionId, 'Xlsx')}
                        >
                          Excel
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}
