import { ChevronRight, FileText } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../../api/client'
import { getReviewSession, markFalseAlarm, type ReviewEvent, type ReviewSessionDetail } from '../../api/review'
import { useAuth } from '../../auth/useAuth'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { DocumentCard } from '../../components/DocumentCard'
import { InfoTip } from '../../components/InfoTip'
import { RiskScore } from '../../components/RiskScore'
import { Seal } from '../../components/Seal'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useResource } from '../../hooks/useResource'
import { DECISION_LABEL, DECISION_TONE, FLAG_LABEL, FLAG_TONE, STATUS_LABEL, STATUS_TONE, eventLabel } from '../../lib/eventCatalog'
import { docNumber, formatDate, formatDateTime, formatTimeRange } from '../../lib/format'
import { ContributionBars } from './ContributionBars'
import { DecisionPanel } from './DecisionPanel'
import { EvidencePanel } from './EvidencePanel'
import { NotesPanel } from './NotesPanel'
import { SignalStrip } from './SignalStrip'
import './review.css'

function timeRange(detail: ReviewSessionDetail): [number, number] {
  const start = new Date(detail.startedAt ?? detail.createdAt).getTime()
  const lastEvent = Math.max(start, ...detail.events.map((item) => new Date(item.startedAt).getTime() + item.durationSec * 1000))
  const end = detail.endedAt ? new Date(detail.endedAt).getTime() : lastEvent
  return [start, Math.max(end, start + 60_000)]
}

/**
 * Oturum detayı ve karar, "vaka dosyası + sinyal kaydı" (Stitch 20–21, Prompt F). Bütün veri
 * `GET /api/review/sessions/{id}`'den; skor ve katkılar sunucunun hesabı. Değerlendirici yanlış alarm işaretleyip
 * karar verir, yönetici yalnızca okur (K-31).
 */
export function SessionDetailPage() {
  const { sessionId = '' } = useParams()
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => getReviewSession(sessionId, signal), [sessionId])
  const { data, error, loading, reload } = useResource(load, 'Dosya yüklenemedi.')
  const [selected, setSelected] = useState<ReviewEvent | null>(null)
  const [cursor, setCursor] = useState<number | null>(null)
  const [markingId, setMarkingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [justDecided, setJustDecided] = useState(false)

  if (loading && !data) {
    return (
      <div className="review-page" aria-busy="true">
        <Skeleton height="8rem" radius="lg" />
        <Skeleton height="18rem" radius="lg" />
      </div>
    )
  }
  if (!data) {
    return (
      <div className="review-page">
        <Alert tone="danger" title="Dosya açılamadı">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      </div>
    )
  }

  const detail = data
  const [start, end] = timeRange(detail)
  const isReviewer = user?.role === 'Reviewer'
  const canDecide = isReviewer && detail.status === 'AwaitingReview'
  const canMark = isReviewer && detail.status !== 'Decided' && !detail.monitoringDataDeletedAt
  // Açılışta skora en çok katkı yapan olay seçili: ayrıntı kartı hiç boş kalmaz.
  const strongest = [...detail.events].sort((a, b) => b.contribution - a.contribution)[0] ?? null
  const selectedEvent = selected ? (detail.events.find((item) => item.id === selected.id) ?? strongest) : strongest

  const select = (event: ReviewEvent) => {
    setSelected(event)
    setCursor(new Date(event.startedAt).getTime())
  }

  // Oynatma çizgisi sürüklenince o ana en yakın olay seçilir (kanıtı olan önce).
  const moveCursor = (time: number) => {
    setCursor(time)
    const nearest = [...detail.events].sort((a, b) => {
      const da = Math.abs(new Date(a.startedAt).getTime() - time) - (a.evidenceUrl ? 5_000 : 0)
      const db = Math.abs(new Date(b.startedAt).getTime() - time) - (b.evidenceUrl ? 5_000 : 0)
      return da - db
    })[0]
    if (nearest && Math.abs(new Date(nearest.startedAt).getTime() - time) < (end - start) / 40) setSelected(nearest)
  }

  const mark = async (event: ReviewEvent) => {
    setMarkingId(event.id)
    setActionError(null)
    try {
      await markFalseAlarm(event.id)
      reload()
    } catch (caught) {
      setActionError(caught instanceof ApiError ? caught.message : 'Yanlış alarm işaretlenemedi.')
    } finally {
      setMarkingId(null)
    }
  }

  const docNo = docNumber(detail.sessionId, detail.startedAt ?? detail.createdAt)

  return (
    <div className="review-page">
      <nav aria-label="Konumunuz" className="breadcrumbs">
        <ol>
          <li>
            <Link to="/review">İncelemeler</Link>
          </li>
          <li aria-hidden="true" className="breadcrumbs-sep">
            <ChevronRight size={14} />
          </li>
          <li>
            <Link to={`/review/exams/${detail.examId}`}>{detail.examName}</Link>
          </li>
          <li aria-hidden="true" className="breadcrumbs-sep">
            <ChevronRight size={14} />
          </li>
          <li aria-current="page" className="breadcrumbs-current">
            {detail.candidateName}
          </li>
        </ol>
      </nav>

      <header className="case-header">
        <div className="case-header-text">
          <h1>{detail.candidateName}</h1>
          <p className="kicker">
            <span className="doc-card-no">Dosya {docNo}</span> · {formatDate(detail.startedAt ?? detail.createdAt)}{' '}
            {formatTimeRange(detail.startedAt ?? detail.createdAt, detail.endedAt)}
          </p>
          <div className="case-header-badges">
            <StatusBadge tone={STATUS_TONE[detail.status]} dot>
              {STATUS_LABEL[detail.status]}
            </StatusBadge>
            {detail.flags
              .filter((flag) => flag !== 'Risky')
              .map((flag) => (
                <StatusBadge key={flag} tone={FLAG_TONE[flag]} stamp>
                  {FLAG_LABEL[flag]}
                </StatusBadge>
              ))}
          </div>
        </div>
        <div className="case-header-score">
          <RiskScore score={detail.riskScore} risky={detail.flags.includes('Risky')} size="xl" />
          {detail.decision ? (
            <div className="case-header-seal">
              <Seal
                label={DECISION_LABEL[detail.decision.decision]}
                ring={detail.decision.reviewerName.toLocaleUpperCase('tr-TR')}
                tone={DECISION_TONE[detail.decision.decision]}
                size={112}
                tilt={-9}
                animate={justDecided}
                title={`Karar: ${DECISION_LABEL[detail.decision.decision]}`}
              />
              <p className="kicker">
                {detail.decision.reviewerName} · {formatDateTime(detail.decision.decidedAt)}
              </p>
            </div>
          ) : null}
        </div>
      </header>

      <p className="case-disclaimer">Tespit edilen olaylar kopya kanıtı değildir; karar size aittir.</p>
      {error ? <Alert tone="warning">{error}</Alert> : null}
      {actionError ? <Alert tone="danger">{actionError}</Alert> : null}

      {detail.monitoringDataDeletedAt ? (
        <Alert tone="info" title="İzleme verileri silindi">
          Saklama süresi dolduğu için {formatDate(detail.monitoringDataDeletedAt)} tarihinde olaylar ve kanıt kareleri silindi. Sonuç ve karar duruyor.
        </Alert>
      ) : (
        <DocumentCard title="Oturum boyunca olaylar" aside={<InfoTip title="Sinyal kaydı nasıl okunur?" align="end"><ul><li>Her satır bir kaynak: kamera, ses, tarayıcı, cihaz, ağ, sistem.</li><li>Bloklar olayın başladığı an ve süresi kadar uzunlukta.</li><li>Üstteki çizgi oturum skorunun zamanla nasıl değiştiğini gösterir.</li><li>Zaman çizgisini sürükleyin ya da bir bloğa tıklayın; ayrıntısı sağda açılır.</li></ul></InfoTip>} className="case-signal">
          <SignalStrip
            start={start}
            end={end}
            events={detail.events}
            scoreHistory={detail.scoreHistory}
            cursor={cursor ?? start}
            selectedId={selectedEvent?.id ?? null}
            onCursor={moveCursor}
            onSelect={select}
          />
          <ul className="signal-legend" aria-label="Gösterim">
            <li className="is-scored">Skora katkı yapan olay</li>
            <li className="is-info">Bilgi (katkısız)</li>
            <li className="is-false-alarm">Yanlış alarm</li>
          </ul>
        </DocumentCard>
      )}

      <div className="case-grid">
        <DocumentCard title={`Skor neden ${Math.round(detail.riskScore)}?`} aside={<InfoTip title="Skor gerekçesi" align="end"><p>Her çubuk bir olay türünün skora toplam katkısı; büyükten küçüğe sıralı. Bir çubuğa tıklayınca o türün en etkili olayı ayrıntıda açılır.</p></InfoTip>}>
          <ContributionBars events={detail.events} selectedType={selectedEvent?.type ?? null} onSelect={select} />
        </DocumentCard>

        <div className="case-side">
          <DocumentCard title={selectedEvent ? `Olay ayrıntısı: ${eventLabel(selectedEvent.type)}` : 'Olay ayrıntısı'}>
            <EvidencePanel
              event={selectedEvent}
              monitoringDataDeletedAt={detail.monitoringDataDeletedAt}
              canMark={canMark}
              marking={markingId === selectedEvent?.id}
              onMark={(event) => void mark(event)}
            />
            <p className="evidence-audit">Bu dosyayı açmanız denetim kaydına yazıldı.</p>
          </DocumentCard>
          <DocumentCard title="Değerlendirici notları">
            <NotesPanel sessionId={detail.sessionId} notes={detail.notes} canWrite={isReviewer} onAdded={reload} />
          </DocumentCard>
        </div>
      </div>

      {canDecide ? (
        <DocumentCard title="Dosyayı karara bağlayın" emphasis aside={<InfoTip title="Karar nasıl verilir?" align="end"><ul><li>Temiz, Şüpheli ya da İhlal seçin; isterseniz gerekçe yazın.</li><li>Onay düğmesini bir saniye basılı tutun.</li><li>Karar bir kez verilir ve değiştirilemez; aday raporunda yer alır.</li></ul></InfoTip>}>
          <DecisionPanel
            sessionId={detail.sessionId}
            onDecided={() => {
              setJustDecided(true)
              reload()
              window.scrollTo({ top: 0, behavior: 'smooth' })
            }}
          />
        </DocumentCard>
      ) : null}

      <div className="case-footer">
        <Link className="button button-secondary button-md" to={`/reports/exams/${detail.examId}/sessions/${detail.sessionId}`}>
          <FileText size={18} aria-hidden="true" />
          Aday raporunu aç
        </Link>
      </div>
    </div>
  )
}
