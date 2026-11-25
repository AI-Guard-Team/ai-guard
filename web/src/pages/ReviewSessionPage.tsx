import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import {
  addSessionNote,
  decideSession,
  getReviewSession,
  markFalseAlarm,
  type DecisionType,
  type ReviewEvent,
  type ReviewSessionDetail,
} from '../api/review'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Button } from '../components/Button'
import { FormAlert } from '../components/AuthCard'
import { Modal } from '../components/Modal'
import { StatusBadge } from '../components/StatusBadge'
import { useToast } from '../components/useToast'
import { RiskChart } from '../review/RiskChart'
import {
  DECISIONS,
  DECISION_LABEL,
  DECISION_TONE,
  FLAG_LABEL,
  FLAG_TONE,
  STATUS_LABEL,
  STATUS_TONE,
  eventLabel,
  formatDateTime,
  formatDuration,
  formatScore,
  formatTime,
  sourceLabel,
} from '../review/labels'

/** Kanıt bağlantıları 5 dakika geçerli (K-31); kare açılmazsa detay en sık bu aralıkla yenilenir. */
const EVIDENCE_REFRESH_MIN_MS = 30_000

type Confirm = { kind: 'decision'; decision: DecisionType } | { kind: 'falseAlarm'; event: ReviewEvent }

function messageOf(error: unknown, fallback: string): string {
  return error instanceof ApiError ? error.message : fallback
}

/** Oturum incelemesi: zaman çizelgesi ve risk grafiği solda; kanıt, not ve karar sağda (FR-28–FR-33). */
export function ReviewSessionPage() {
  const { sessionId } = useParams()
  const { user } = useAuth()
  const showToast = useToast()
  const [detail, setDetail] = useState<ReviewSessionDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<Confirm | null>(null)
  const [decisionNote, setDecisionNote] = useState('')
  const [noteText, setNoteText] = useState('')
  const [pending, setPending] = useState(false)
  const lastEvidenceRefresh = useRef(0)

  // Karar, not ve yanlış alarm yalnızca atanmış değerlendiriciye açık; Admin yalnızca okur (K-31).
  const canWrite = user?.role === 'Reviewer'

  const load = useCallback(
    async (signal?: AbortSignal) => {
      if (!sessionId) return
      try {
        const result = await getReviewSession(sessionId, signal)
        if (signal?.aborted) return
        setDetail(result)
        setError(null)
      } catch (reason) {
        if (signal?.aborted || (reason instanceof DOMException && reason.name === 'AbortError')) return
        setError(messageOf(reason, 'Oturum yüklenemedi.'))
      }
    },
    [sessionId],
  )

  useEffect(() => {
    const controller = new AbortController()
    // eslint-disable-next-line react-hooks/set-state-in-effect -- veri çekme: sonuç asenkron yazılıyor
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  const refreshExpiredEvidence = () => {
    const now = Date.now()
    if (now - lastEvidenceRefresh.current < EVIDENCE_REFRESH_MIN_MS) return
    lastEvidenceRefresh.current = now
    void load()
  }

  const confirmAction = async () => {
    if (!confirm || !sessionId) return
    setPending(true)
    try {
      if (confirm.kind === 'decision') {
        await decideSession(sessionId, confirm.decision, decisionNote)
        showToast('Karar kaydedildi.', 'success')
        setDecisionNote('')
      } else {
        await markFalseAlarm(confirm.event.id)
        showToast('Olay yanlış alarm olarak işaretlendi.', 'success')
      }
      setConfirm(null)
      await load()
    } catch (reason) {
      setConfirm(null)
      showToast(messageOf(reason, 'İşlem tamamlanamadı. Lütfen tekrar deneyin.'), 'error')
      // 409'da (ör. karar zaten verilmiş) ekran güncel duruma getirilir.
      if (reason instanceof ApiError && reason.status === 409) await load()
    } finally {
      setPending(false)
    }
  }

  const submitNote = async (event: FormEvent) => {
    event.preventDefault()
    if (!sessionId || !noteText.trim()) return
    setPending(true)
    try {
      await addSessionNote(sessionId, noteText.trim())
      setNoteText('')
      showToast('Not eklendi.', 'success')
      await load()
    } catch (reason) {
      showToast(messageOf(reason, 'Not eklenemedi.'), 'error')
    } finally {
      setPending(false)
    }
  }

  if (error && !detail) {
    return (
      <section className="card review-wide">
        <p>
          <Link to="/review">← Sınavlar</Link>
        </p>
        <FormAlert message={error} />
      </section>
    )
  }
  if (!detail) {
    return (
      <section className="card review-wide">
        <p>Yükleniyor…</p>
      </section>
    )
  }

  const evidence = detail.events.filter((item) => item.evidenceUrl)
  const awaiting = detail.status === 'AwaitingReview'
  const decided = detail.status === 'Decided'

  return (
    <section className="card review-wide">
      <p>
        <Link to={`/review/exams/${detail.examId}`}>← {detail.examName}</Link>
      </p>
      <h1>{detail.candidateName}</h1>
      <p>
        {/* Aday rapor ekranı E6 kartında (Esma); o sayfanın adresi bu rotaya bağlanacak. */}
        <Link to={`/review/reports/${detail.sessionId}`}>Aday raporu</Link>
      </p>
      <p className="review-summary">
        <StatusBadge tone={STATUS_TONE[detail.status]}>{STATUS_LABEL[detail.status]}</StatusBadge>
        {detail.flags.map((flag) => (
          <StatusBadge key={flag} tone={FLAG_TONE[flag]}>
            {FLAG_LABEL[flag]}
          </StatusBadge>
        ))}
        <span>
          Risk skoru: {formatScore(detail.riskScore)} · {detail.flags.includes('Risky') ? 'Riskli' : 'Riskli değil'}
        </span>
        <span>Başlangıç: {formatDateTime(detail.startedAt)}</span>
        <span>Bitiş: {formatDateTime(detail.endedAt)}</span>
      </p>

      <div className="review-columns">
        <div>
          <h2>Risk grafiği</h2>
          {detail.monitoringDataDeletedAt ? (
            <p className="table-empty">
              İzleme verileri saklama süresi dolduğu için {formatDateTime(detail.monitoringDataDeletedAt)} tarihinde silindi. Skor,
              işaretler ve karar kayıtlı hâliyle duruyor.
            </p>
          ) : (
            <RiskChart points={detail.scoreHistory} />
          )}

          <h2>Olaylar</h2>
          {detail.events.length === 0 ? (
            <p className="table-empty">Gösterilecek olay yok.</p>
          ) : (
            <ol className="event-timeline">
              {detail.events.map((item) => (
                <li key={item.id} className={item.isFalseAlarm ? 'is-false-alarm' : undefined}>
                  <div className="event-head">
                    <strong>{eventLabel(item.type)}</strong>
                    <span>{formatTime(item.startedAt)}</span>
                    <StatusBadge tone="neutral">{sourceLabel(item.source)}</StatusBadge>
                    {item.isFalseAlarm ? <StatusBadge tone="info">Yanlış alarm</StatusBadge> : null}
                  </div>
                  <p className="event-meta">
                    Süre: {formatDuration(item.durationSec)} · Katkı: {formatScore(item.contribution)}
                    {item.confidence != null ? ` · Güven: %${Math.round(item.confidence * 100)}` : ''}
                  </p>
                  {item.reason ? <p className="event-meta">{item.reason}</p> : null}
                  {item.isFalseAlarm && item.falseAlarmMarkedBy ? (
                    <p className="event-meta">
                      {item.falseAlarmMarkedBy} işaretledi, {formatDateTime(item.falseAlarmMarkedAt)}
                    </p>
                  ) : null}
                  {canWrite && !decided && !item.isFalseAlarm ? (
                    <Button variant="secondary" onClick={() => setConfirm({ kind: 'falseAlarm', event: item })}>
                      Yanlış alarm
                    </Button>
                  ) : null}
                </li>
              ))}
            </ol>
          )}
        </div>

        <div>
          <h2>Kanıt kareleri</h2>
          {evidence.length === 0 ? (
            <p className="table-empty">Kanıt karesi yok.</p>
          ) : (
            <ul className="evidence-grid">
              {evidence.map((item) => (
                <li key={item.id}>
                  <img
                    src={item.evidenceUrl ?? undefined}
                    alt={`${eventLabel(item.type)} kanıt karesi, ${formatTime(item.startedAt)}`}
                    onError={refreshExpiredEvidence}
                  />
                  <span>
                    {eventLabel(item.type)} · {formatTime(item.startedAt)}
                  </span>
                </li>
              ))}
            </ul>
          )}

          <h2>Karar</h2>
          {detail.decision ? (
            <div className="review-decision">
              <p>
                <StatusBadge tone={DECISION_TONE[detail.decision.decision]}>{DECISION_LABEL[detail.decision.decision]}</StatusBadge>
              </p>
              {detail.decision.note ? <p>{detail.decision.note}</p> : null}
              <p className="event-meta">
                {detail.decision.reviewerName}, {formatDateTime(detail.decision.decidedAt)}
              </p>
            </div>
          ) : awaiting && canWrite ? (
            <div>
              <div className="field">
                <label htmlFor="decision-note">Karar notu (isteğe bağlı)</label>
                <textarea id="decision-note" rows={3} maxLength={4000} value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} />
              </div>
              <div className="action-row">
                {DECISIONS.map((decision) => (
                  <Button
                    key={decision}
                    variant={decision === 'Violation' ? 'danger' : decision === 'Clean' ? 'primary' : 'secondary'}
                    onClick={() => setConfirm({ kind: 'decision', decision })}
                  >
                    {DECISION_LABEL[decision]}
                  </Button>
                ))}
              </div>
            </div>
          ) : (
            <p className="table-empty">
              {awaiting ? 'Karar yalnızca atanmış değerlendirici tarafından verilir.' : 'Bu oturum için karar gerekmiyor.'}
            </p>
          )}

          <h2>Notlar</h2>
          {detail.notes.length === 0 ? (
            <p className="table-empty">Henüz not yok.</p>
          ) : (
            <ul className="note-list">
              {detail.notes.map((note) => (
                <li key={note.id}>
                  <p>{note.text}</p>
                  <p className="event-meta">
                    {note.authorName}, {formatDateTime(note.createdAt)}
                  </p>
                </li>
              ))}
            </ul>
          )}
          {canWrite ? (
            <form onSubmit={submitNote}>
              <div className="field">
                <label htmlFor="session-note">Yeni not</label>
                <textarea id="session-note" rows={3} maxLength={4000} value={noteText} onChange={(event) => setNoteText(event.target.value)} />
              </div>
              <Button type="submit" variant="secondary" disabled={pending || !noteText.trim()}>
                Not ekle
              </Button>
            </form>
          ) : null}
        </div>
      </div>

      <Modal
        open={confirm !== null}
        title={confirm?.kind === 'decision' ? 'Kararı onayla' : 'Yanlış alarm olarak işaretle'}
        onClose={() => (pending ? undefined : setConfirm(null))}
      >
        {confirm?.kind === 'decision' ? (
          <p>
            Karar <strong>{DECISION_LABEL[confirm.decision]}</strong> olarak kaydedilecek. Karar bir kez verilir ve değiştirilemez.
          </p>
        ) : confirm?.kind === 'falseAlarm' ? (
          <p>
            <strong>{eventLabel(confirm.event.type)}</strong> olayı skordan çıkarılacak. Olay silinmez, ama işaret geri alınamaz.
          </p>
        ) : null}
        <div className="action-row">
          <Button onClick={() => void confirmAction()} disabled={pending}>
            {pending ? 'Kaydediliyor…' : 'Onayla'}
          </Button>
          <Button variant="secondary" onClick={() => setConfirm(null)} disabled={pending}>
            Vazgeç
          </Button>
        </div>
      </Modal>
    </section>
  )
}
