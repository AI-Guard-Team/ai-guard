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
import { Modal } from '../../components/Modal'
import { RiskScore } from '../../components/RiskScore'
import { Seal } from '../../components/Seal'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useResource } from '../../hooks/useResource'
import { DECISION_LABEL, DECISION_TONE, FLAG_LABEL, FLAG_TONE, STATUS_LABEL, STATUS_TONE, eventLabel } from '../../lib/eventCatalog'
import { docNumber, formatDate, formatDateTime, formatTimeRange, formatTimeSeconds } from '../../lib/format'
import { common } from '../../i18n/common'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { ContributionBars } from './ContributionBars'
import { DecisionPanel } from './DecisionPanel'
import { EvidencePanel } from './EvidencePanel'
import { NotesPanel } from './NotesPanel'
import { SignalStrip } from './SignalStrip'
import './review.css'

const messages = defineMessages({
  tr: {
    failed: 'Dosya yüklenemedi.',
    openFailed: 'Dosya açılamadı',
    markFailed: 'Yanlış alarm işaretlenemedi.',
    location: 'Konumunuz',
    reviews: 'İncelemeler',
    file: (no: string) => `Dosya ${no}`,
    decision: (label: string) => `Karar: ${label}`,
    disclaimer: 'Tespit edilen olaylar kopya kanıtı değildir; karar size aittir.',
    deletedTitle: 'İzleme verileri silindi',
    deletedText: (date: string) => `Saklama süresi dolduğu için ${date} tarihinde olaylar ve kanıt kareleri silindi. Sonuç ve karar duruyor.`,
    events: 'Oturum boyunca olaylar',
    howToRead: 'Sinyal kaydı nasıl okunur?',
    howToReadItems: [
      'Her satır bir kaynak: kamera, ses, tarayıcı, cihaz, ağ, sistem.',
      'Bloklar olayın başladığı an ve süresi kadar uzunlukta.',
      'Üstteki çizgi oturum skorunun zamanla nasıl değiştiğini gösterir.',
      'Zaman çizgisini sürükleyin ya da bir bloğa tıklayın; ayrıntısı sağda açılır.',
    ],
    legend: 'Gösterim',
    scored: 'Skora katkı yapan olay',
    info: 'Bilgi (katkısız)',
    falseAlarm: 'Yanlış alarm',
    why: (score: number) => `Skor neden ${score}?`,
    whyTitle: 'Skor gerekçesi',
    whyText: 'Her çubuk bir olay türünün skora toplam katkısı; büyükten küçüğe sıralı. Bir çubuğa tıklayınca o türün en etkili olayı ayrıntıda açılır.',
    eventDetail: 'Olay ayrıntısı',
    eventDetailOf: (label: string) => `Olay ayrıntısı: ${label}`,
    audited: 'Bu dosyayı açmanız denetim kaydına yazıldı.',
    notes: 'Değerlendirici notları',
    decide: 'Dosyayı karara bağlayın',
    howToDecide: 'Karar nasıl verilir?',
    howToDecideItems: [
      'Temiz, Şüpheli ya da İhlal seçin; isterseniz gerekçe yazın.',
      'Onay düğmesini bir saniye basılı tutun.',
      'Karar bir kez verilir ve değiştirilemez; aday raporunda yer alır.',
    ],
    markTitle: 'Yanlış alarm olarak işaretlensin mi?',
    markText: (event: string) => `${event}olayı silinmez, skordan çıkar ve işaretleyen olarak adınız kaydedilir. Bu işlem geri alınamaz.`,
    mark: 'İşaretle',
    report: 'Aday raporunu aç',
  },
  en: {
    failed: 'The file could not be loaded.',
    openFailed: 'The file could not be opened',
    markFailed: 'The false alarm could not be marked.',
    location: 'You are here',
    reviews: 'Reviews',
    file: (no: string) => `File ${no}`,
    decision: (label: string) => `Decision: ${label}`,
    disclaimer: 'Detected events are not proof of cheating; the decision is yours.',
    deletedTitle: 'Monitoring data deleted',
    deletedText: (date: string) => `Because the retention period ended, events and evidence frames were deleted on ${date}. The result and decision remain.`,
    events: 'Events during the session',
    howToRead: 'How to read the signal log',
    howToReadItems: [
      'Each row is a source: camera, audio, browser, device, network, system.',
      'Blocks start when the event started and are as long as it lasted.',
      'The line at the top shows how the session score changed over time.',
      'Drag the timeline or click a block; its details open on the right.',
    ],
    legend: 'Legend',
    scored: 'Event contributing to the score',
    info: 'Information (no contribution)',
    falseAlarm: 'False alarm',
    why: (score: number) => `Why is the score ${score}?`,
    whyTitle: 'Score explanation',
    whyText: 'Each bar is an event type’s total contribution to the score, from largest to smallest. Click a bar to open that type’s most influential event.',
    eventDetail: 'Event details',
    eventDetailOf: (label: string) => `Event details: ${label}`,
    audited: 'Opening this file was written to the audit log.',
    notes: 'Reviewer notes',
    decide: 'Decide this file',
    howToDecide: 'How do I decide?',
    howToDecideItems: [
      'Choose Clean, Suspicious or Violation; add a justification if you like.',
      'Press and hold the confirm button for one second.',
      'A decision is made once and cannot be changed; it appears in the candidate report.',
    ],
    markTitle: 'Mark as a false alarm?',
    markText: (event: string) => `The ${event}event is not deleted; it leaves the score and your name is recorded as the person who marked it. This cannot be undone.`,
    mark: 'Mark',
    report: 'Open candidate report',
  },
})

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
  const m = useMessages(messages)
  const c = useMessages(common)
  const { sessionId = '' } = useParams()
  const { user } = useAuth()
  const load = useCallback((signal: AbortSignal) => getReviewSession(sessionId, signal), [sessionId])
  const { data, error, loading, reload } = useResource(load, m.failed)
  const [selected, setSelected] = useState<ReviewEvent | null>(null)
  const [cursor, setCursor] = useState<number | null>(null)
  const [markingId, setMarkingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [justDecided, setJustDecided] = useState(false)
  // Yanlış alarm geri alınamaz: önce onay penceresi (S5'teki karar, K-34).
  const [confirmMark, setConfirmMark] = useState<ReviewEvent | null>(null)

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
        <Alert tone="danger" title={m.openFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
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
    if (markingId) return
    setMarkingId(event.id)
    setActionError(null)
    try {
      await markFalseAlarm(event.id)
      reload()
    } catch (caught) {
      setActionError(caught instanceof ApiError ? caught.message : m.markFailed)
    } finally {
      setMarkingId(null)
    }
  }

  const docNo = docNumber(detail.sessionId, detail.startedAt ?? detail.createdAt)

  return (
    <div className="review-page">
      <nav aria-label={m.location} className="breadcrumbs">
        <ol>
          <li>
            <Link to="/review">{m.reviews}</Link>
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
            <span className="doc-card-no">{m.file(docNo)}</span> · {formatDate(detail.startedAt ?? detail.createdAt)}{' '}
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
                ring={detail.decision.reviewerName.toLocaleUpperCase(getLocale())}
                tone={DECISION_TONE[detail.decision.decision]}
                size={112}
                tilt={-9}
                animate={justDecided}
                title={m.decision(DECISION_LABEL[detail.decision.decision])}
              />
              <p className="kicker">
                {detail.decision.reviewerName} · {formatDateTime(detail.decision.decidedAt)}
              </p>
            </div>
          ) : null}
        </div>
      </header>

      <p className="case-disclaimer">{m.disclaimer}</p>
      {error ? <Alert tone="warning">{error}</Alert> : null}
      {actionError ? <Alert tone="danger">{actionError}</Alert> : null}

      {detail.monitoringDataDeletedAt ? (
        <Alert tone="info" title={m.deletedTitle}>
          {m.deletedText(formatDate(detail.monitoringDataDeletedAt))}
        </Alert>
      ) : (
        <DocumentCard
          title={m.events}
          aside={
            <InfoTip title={m.howToRead} align="end">
              <ul>
                {m.howToReadItems.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </InfoTip>
          }
          className="case-signal"
        >
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
          <ul className="signal-legend" aria-label={m.legend}>
            <li className="is-scored">{m.scored}</li>
            <li className="is-info">{m.info}</li>
            <li className="is-false-alarm">{m.falseAlarm}</li>
          </ul>
        </DocumentCard>
      )}

      <div className="case-grid">
        <DocumentCard
          title={m.why(Math.round(detail.riskScore))}
          aside={
            <InfoTip title={m.whyTitle} align="end">
              <p>{m.whyText}</p>
            </InfoTip>
          }
        >
          <ContributionBars events={detail.events} selectedType={selectedEvent?.type ?? null} onSelect={select} />
        </DocumentCard>

        <div className="case-side">
          <DocumentCard title={selectedEvent ? m.eventDetailOf(eventLabel(selectedEvent.type)) : m.eventDetail}>
            <EvidencePanel
              event={selectedEvent}
              monitoringDataDeletedAt={detail.monitoringDataDeletedAt}
              canMark={canMark}
              marking={markingId === selectedEvent?.id}
              onMark={setConfirmMark}
            />
            <p className="evidence-audit">{m.audited}</p>
          </DocumentCard>
          <DocumentCard title={m.notes}>
            <NotesPanel sessionId={detail.sessionId} notes={detail.notes} canWrite={isReviewer} onAdded={reload} />
          </DocumentCard>
        </div>
      </div>

      {canDecide ? (
        <DocumentCard
          title={m.decide}
          emphasis
          aside={
            <InfoTip title={m.howToDecide} align="end">
              <ul>
                {m.howToDecideItems.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </InfoTip>
          }
        >
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

      <Modal open={confirmMark !== null} title={m.markTitle} onClose={() => setConfirmMark(null)}>
        <p>{m.markText(confirmMark ? `${eventLabel(confirmMark.type)} (${formatTimeSeconds(confirmMark.startedAt)}) ` : '')}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmMark(null)}>
            {c.cancel}
          </Button>
          <Button
            loading={markingId !== null}
            onClick={() => {
              const event = confirmMark
              setConfirmMark(null)
              if (event) void mark(event)
            }}
          >
            {m.mark}
          </Button>
        </div>
      </Modal>

      <div className="case-footer">
        <Link className="button button-secondary button-md" to={`/reports/exams/${detail.examId}/sessions/${detail.sessionId}`}>
          <FileText size={18} aria-hidden="true" />
          {m.report}
        </Link>
      </div>
    </div>
  )
}
