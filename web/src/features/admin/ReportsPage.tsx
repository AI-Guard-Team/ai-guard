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
import { formatDateTime } from '../../lib/format'
import './admin.css'

const loadExams = (signal: AbortSignal) => listExams(signal)

/**
 * Raporlar (Stitch 30): sınav seçilir, o sınavın oturumları listelenir; her aday için rapor açılır ya da PDF/Excel
 * indirilir. Yönetici raporları yalnızca okur (K-31); rapor görüntüleme ve indirme denetim kaydına yazılır.
 */
export function ReportsPage() {
  const exams = useResource(loadExams, 'Sınavlar yüklenemedi.')
  const [examId, setExamId] = useState<string>('')
  const selected = examId || exams.data?.[0]?.id || ''

  const loadSessions = useCallback((signal: AbortSignal) => (selected ? getReviewSessions(selected, { sort: 'StartedAt' }, signal) : Promise.resolve(null)), [selected])
  const sessions = useResource(loadSessions, 'Oturumlar yüklenemedi.')
  const showToast = useToast()
  const [downloading, setDownloading] = useState<string | null>(null)

  const download = async (sessionId: string, format: ReportFormat) => {
    setDownloading(`${sessionId}-${format}`)
    try {
      await downloadCandidateReport(selected, sessionId, format)
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Rapor indirilemedi.', 'error')
    } finally {
      setDownloading(null)
    }
  }

  const rows = sessions.data?.sessions ?? []

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }]}
        title="Raporlar"
        description="Her adayın sonucu, risk skoru, olayları ve değerlendirici kararı."
        info={
          <ul>
            <li>Önce sınavı seçin; o sınava giren adayların oturumları listelenir.</li>
            <li>"Raporu aç" ekranda belge görünümünü açar; PDF ve Excel aynı raporun dosyasıdır.</li>
            <li>Rapor görüntüleme ve indirme denetim kaydına yazılır.</li>
          </ul>
        }
      />

      {exams.error && !exams.data ? <Alert tone="danger">{exams.error}</Alert> : null}

      {exams.loading && !exams.data ? (
        <Skeleton height="16rem" radius="lg" />
      ) : exams.data && exams.data.length === 0 ? (
        <EmptyState icon={FileBarChart} title="Henüz sınav yok">
          Sınav oluşturulup adaylar girdikçe raporlar burada görünür.
        </EmptyState>
      ) : (
        <>
          <div className="admin-toolbar">
            <label className="field field-inline">
              <span className="field-label">Sınav</span>
              <select className="select" value={selected} onChange={(event) => setExamId(event.target.value)}>
                {exams.data?.map((exam) => (
                  <option key={exam.id} value={exam.id}>
                    {exam.name}
                  </option>
                ))}
              </select>
            </label>
            {sessions.data ? <p className="admin-row-sub">{rows.length} oturum</p> : null}
          </div>

          {sessions.error ? <Alert tone="danger">{sessions.error}</Alert> : null}

          {sessions.loading && !sessions.data ? (
            <Skeleton height="14rem" radius="lg" />
          ) : rows.length === 0 ? (
            <EmptyState icon={FileText} title="Bu sınavda oturum yok">
              Adaylar sınava girdikçe oturumları ve raporları burada listelenir.
            </EmptyState>
          ) : (
            <div className="table-card">
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">Aday</th>
                    <th scope="col">Başlangıç</th>
                    <th scope="col">Durum</th>
                    <th scope="col">Risk</th>
                    <th scope="col">Karar</th>
                    <th scope="col">
                      <span className="visually-hidden">Rapor</span>
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
                      <td>{row.decision ? <StatusBadge tone={DECISION_TONE[row.decision]}>{DECISION_LABEL[row.decision]}</StatusBadge> : <span className="admin-row-sub">—</span>}</td>
                      <td className="cell-actions">
                        <Link className="button button-secondary button-md" to={`/reports/exams/${selected}/sessions/${row.sessionId}`}>
                          <FileText size={16} aria-hidden="true" />
                          Raporu aç
                        </Link>
                        <Button
                          variant="ghost"
                          aria-label={`${row.candidateName} raporunu PDF olarak indir`}
                          loading={downloading === `${row.sessionId}-Pdf`}
                          icon={<Download size={16} aria-hidden="true" />}
                          onClick={() => void download(row.sessionId, 'Pdf')}
                        >
                          PDF
                        </Button>
                        <Button
                          variant="ghost"
                          aria-label={`${row.candidateName} raporunu Excel olarak indir`}
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
