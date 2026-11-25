import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api/client'
import {
  SESSION_POLL_MS,
  getAgentStatus,
  getSession,
  saveAnswer,
  submitExam,
  verifyIdentity,
  type AgentStatus,
  type SessionAnswer,
  type SessionQuestion,
  type SessionView,
} from '../api/sessions'
import { Button } from '../components/Button'
import { FormAlert } from '../components/Alert'
import { Modal } from '../components/Modal'
import {
  chosenOptions,
  enqueue,
  flushAnswers,
  listPending,
  nextSeq,
  removePending,
  type PendingAnswer,
} from '../exam/answerQueue'
import { formatRemaining, remainingMs } from '../exam/remainingTime'
import { useExamMonitoring } from '../exam/useExamMonitoring'
import { captureFrames, useCamera } from '../features/camera/useCamera'

const ANSWERABLE = new Set(['InProgress', 'Disconnected'])
const RUNNING = new Set(['InProgress', 'PermissionPending', 'Disconnected'])
const FINISHED = new Set(['Completed', 'Decided'])

const SAVED_LOCALLY = 'Cevap bu cihaza kaydedildi. Bağlantı gelince gönderilecek.'

function keptForLater(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504)
}

interface ClockAnchor {
  endsAt: string
  serverTime: string
}

function agentText(status: AgentStatus | null): string {
  if (!status) return '…'
  if (status.agentActive) return 'Çalışıyor'
  if (status.paired) return 'Sinyal yok'
  return 'Yok'
}

function cameraText(sessionState: string | undefined, live: boolean, permission: PermissionState | null): string {
  if (sessionState === 'PermissionPending' || permission === 'denied') return 'İzin yok'
  if (live || permission === 'granted') return 'Açık'
  return 'Kapalı'
}

function messageOf(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error && error.message) return error.message
  return fallback
}

export function ExamPage() {
  const { sessionId } = useParams()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const lastSeq = useRef(0)
  const savedAnswers = useRef<SessionAnswer[]>([])
  const chain = useRef(Promise.resolve())
  const offline = useRef(false)
  const sending = useRef(new Set<number>())
  const [loading, setLoading] = useState(true)
  const [view, setView] = useState<SessionView | null>(null)
  const [questions, setQuestions] = useState<SessionQuestion[]>([])
  const [questionIndex, setQuestionIndex] = useState(0)
  const [selections, setSelections] = useState<Map<string, number | null>>(new Map())
  const [queued, setQueued] = useState(0)
  const [anchor, setAnchor] = useState<ClockAnchor | null>(null)
  const [elapsed, setElapsed] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [connected, setConnected] = useState(true)
  const [agent, setAgent] = useState<AgentStatus | null>(null)
  const [cameraPermission, setCameraPermission] = useState<PermissionState | null>(null)
  const [fullscreen, setFullscreen] = useState(() => typeof document !== 'undefined' && document.fullscreenElement != null)

  const exclusive = useCallback(<T,>(task: () => Promise<T>): Promise<T> => {
    const run = chain.current.then(task, task)
    chain.current = run.then(
      () => undefined,
      () => undefined,
    )
    return run
  }, [])

  const refreshSelections = useCallback(async (id: string) => {
    const pendingAnswers = await listPending(id)
    const waiting = pendingAnswers.filter((item) => !sending.current.has(item.seq))
    setQueued(waiting.length)
    setSelections(chosenOptions(savedAnswers.current, pendingAnswers))
  }, [])

  const deliver = useCallback(
    async (answer: PendingAnswer): Promise<'sent' | 'drop' | 'stop'> => {
      try {
        await saveAnswer(answer.sessionId, {
          questionId: answer.questionId,
          selectedOption: answer.selectedOption,
          seq: answer.seq,
          clientTime: answer.clientTime,
        })
        await removePending(answer)
        lastSeq.current = Math.max(lastSeq.current, answer.seq)
        savedAnswers.current = [
          ...savedAnswers.current,
          { questionId: answer.questionId, selectedOption: answer.selectedOption, seq: answer.seq },
        ]
        return 'sent'
      } catch (caught) {
        if (keptForLater(caught)) {
          offline.current = true
          return 'stop'
        }
        setError(messageOf(caught, 'Cevap kaydedilemedi.'))
        await removePending(answer)
        return 'drop'
      }
    },
    [],
  )

  const flush = useCallback(async (): Promise<boolean> => {
    if (!sessionId) return true
    const { stopped } = await flushAnswers(await listPending(sessionId), deliver)
    await refreshSelections(sessionId)
    if (stopped && offline.current) {
      setNotice(SAVED_LOCALLY)
    }
    return !stopped
  }, [deliver, refreshSelections, sessionId])

  const apply = useCallback(
    async (next: SessionView) => {
      savedAnswers.current = next.answers
      lastSeq.current = Math.max(lastSeq.current, next.lastSeq)
      setView(next)
      if (next.questions.length > 0) setQuestions(next.questions)
      if (next.endsAt) {
        setAnchor({ endsAt: next.endsAt, serverTime: next.serverTime })
        setElapsed(0)
      }
      if (sessionId) await refreshSelections(sessionId)
    },
    [refreshSelections, sessionId],
  )

  const pull = useCallback(async () => {
    if (!sessionId) return
    try {
      let next = await getSession(sessionId)
      if (!next) {
        setError('Oturum bulunamadı.')
        return
      }
      const pendingAnswers = await listPending(sessionId)
      if (offline.current || next.state === 'Disconnected' || pendingAnswers.length > 0) {
        const flushed = await flush()
        if (!flushed) return
        offline.current = false
        // Kuyruk gittikten sonra sunucudaki cevaplar ve seq yeniden okunur.
        next = (await getSession(sessionId)) ?? next
      }
      setNotice(null)
      setConnected(true)
      await apply(next)
      void getAgentStatus(sessionId)
        .then((status) => {
          if (status && typeof status.agentActive === 'boolean') setAgent(status)
        })
        .catch(() => undefined)
    } catch (caught) {
      if (keptForLater(caught)) {
        offline.current = true
        setConnected(false)
        setNotice(SAVED_LOCALLY)
        return
      }
      setError(messageOf(caught, 'Oturum alınamadı.'))
    } finally {
      setLoading(false)
    }
  }, [apply, flush, sessionId])

  useEffect(() => {
    const permissions = navigator.permissions
    if (!permissions?.query) return
    let current: PermissionStatus | undefined
    let alive = true
    void permissions
      .query({ name: 'camera' as PermissionName })
      .then((status) => {
        if (!alive) return
        current = status
        setCameraPermission(status.state)
        status.onchange = () => setCameraPermission(status.state)
      })
      .catch(() => undefined)
    return () => {
      alive = false
      if (current) current.onchange = null
    }
  }, [])

  // S4: kamera/mikrofon akışı, izin ve tarayıcı olayları.
  const monitoring = useExamMonitoring({ sessionId, state: view?.state, onStateChange: () => void pull() })

  useEffect(() => {
    const onChange = () => setFullscreen(document.fullscreenElement != null)
    document.addEventListener('fullscreenchange', onChange)
    return () => document.removeEventListener('fullscreenchange', onChange)
  }, [])

  const enterFullscreen = () => {
    void document.documentElement.requestFullscreen?.().catch(() => undefined)
  }

  useEffect(() => {
    if (!sessionId) return
    const timer = window.setInterval(() => void pull(), SESSION_POLL_MS)
    const clock = window.setInterval(() => setElapsed((value) => value + 1000), 1000)
    const onOnline = () => void pull()
    window.addEventListener('online', onOnline)
    const started = window.setTimeout(() => void pull(), 0)
    return () => {
      window.clearTimeout(started)
      window.clearInterval(timer)
      window.clearInterval(clock)
      window.removeEventListener('online', onOnline)
    }
  }, [pull, sessionId])

  const choose = (questionId: string, selectedOption: number) => {
    if (!sessionId || !view || !ANSWERABLE.has(view.state)) return
    void exclusive(async () => {
      setError(null)
      const answer: PendingAnswer = {
        sessionId,
        questionId,
        selectedOption,
        seq: nextSeq(lastSeq.current, await listPending(sessionId)),
        clientTime: new Date().toISOString(),
      }
      await enqueue(answer)
      sending.current.add(answer.seq)
      try {
        await refreshSelections(sessionId)
        const sent = await deliver(answer)
        if (sent === 'stop' && offline.current) {
          setNotice(SAVED_LOCALLY)
        }
      } finally {
        sending.current.delete(answer.seq)
        await refreshSelections(sessionId)
      }
    })
  }

  const verifyAgain = async () => {
    const video = videoRef.current
    if (!sessionId || !camera.live || !video) {
      setError('Önce kamerayı açın.')
      return
    }
    setPending(true)
    setError(null)
    try {
      // Kesik oturumda cevaplar yeniden doğrulamadan önce gider; sunucu onları izlenmeyen diye işaretler.
      if (!(await flush())) return
      const frames = await captureFrames(video)
      // Başarısız doğrulama 422 ile hata olarak gelir (A3); yanıtta "passed" alanı yok.
      await verifyIdentity(sessionId, frames)
      camera.stop()
      await pull()
    } catch (caught) {
      setError(messageOf(caught, 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.'))
    } finally {
      setPending(false)
    }
  }

  const submit = async () => {
    if (!sessionId) return
    setPending(true)
    setError(null)
    try {
      if (!(await flush())) return
      await submitExam(sessionId)
      setConfirming(false)
      await pull()
    } catch (caught) {
      setError(messageOf(caught, 'Sınav teslim edilemedi.'))
    } finally {
      setPending(false)
    }
  }

  if (!sessionId) {
    return (
      <section className="card">
        <h1>Sınav</h1>
        <p>Oturum bulunamadı.</p>
      </section>
    )
  }

  const remaining = anchor ? remainingMs(anchor.endsAt, anchor.serverTime, elapsed) : null
  const answering = view ? ANSWERABLE.has(view.state) && (remaining === null || remaining > 0) : false
  const index = questions.length === 0 ? 0 : Math.min(questionIndex, questions.length - 1)
  const question = questions[index]

  return (
    <section className="card exam-card">
      <div className="exam-bar">
        <div>
          <h1>{view?.examName ?? 'Sınav'}</h1>
          <ul className="exam-status" aria-label="Sınav durumu">
            <li>Kamera {cameraText(view?.state, camera.live || monitoring.live, cameraPermission)}</li>
            <li>Ajan {agentText(agent)}</li>
            <li>Bağlantı {!connected || view?.state === 'Disconnected' ? 'Kesik' : 'Bağlı'}</li>
          </ul>
        </div>
        <p className="exam-timer">Kalan süre {remaining !== null ? formatRemaining(remaining) : '—'}</p>
      </div>
      <FormAlert message={error} />
      {notice ? <p className="field-hint is-danger">{notice}</p> : null}
      {/* S4: kamera/mikrofon açılamadıysa ya da tarayıcı desteklemiyorsa. */}
      {view?.state === 'InProgress' ? <FormAlert message={monitoring.error} /> : null}

      {/* S4: FULLSCREEN_EXIT ancak tam ekrana girilmişse oluşur; tarayıcı tam ekranı yalnızca tıklamayla açıyor. */}

      {!loading && view?.state === 'InProgress' && !fullscreen && document.fullscreenEnabled ? (
        <div className="action-row">
          <Button variant="secondary" onClick={enterFullscreen}>
            Tam ekrana geç
          </Button>
        </div>
      ) : null}

      {loading ? <p>Yükleniyor…</p> : null}

      {!loading && view && FINISHED.has(view.state) ? <p>Sınav teslim edildi.</p> : null}

      {/* S4, FR-17: izin beklerken sorular sunucudan gelmiyor; süre işliyor, aday izni buradan yeniden verir. */}
      {!loading && view?.state === 'PermissionPending' ? (
        <div>
          <p>
            Kamera ve mikrofon izni geri alındığı için sorular gizlendi. Süre işlemeye devam ediyor. İzin verince sorular
            yeniden açılır.
          </p>
          <FormAlert message={monitoring.error} />
          <div className="action-row">
            <Button disabled={monitoring.requesting} onClick={() => void monitoring.requestPermission()}>
              {monitoring.requesting ? 'İzin bekleniyor…' : 'Kamera ve mikrofona izin ver'}
            </Button>
          </div>
        </div>
      ) : null}

      {!loading && view?.state === 'Disconnected' ? (
        <div>
          <p>
            Bağlantı kesildi.{' '}
            {queued > 0
              ? 'Kayıtlı cevaplar yeniden doğrulamadan önce gönderilecek.'
              : 'Kayıtlı cevaplar gönderildi.'}{' '}
            Sınava dönmek için kimliğinizi yeniden doğrulayın.
          </p>
          <video ref={videoRef} className={camera.live ? 'camera-preview' : 'visually-hidden'} muted playsInline />
          {camera.error ? <FormAlert message={camera.error} /> : null}
          <div className="action-row">
            <Button variant="secondary" disabled={pending} onClick={() => void camera.start()}>
              Kamerayı aç
            </Button>
            <Button disabled={pending || !camera.live} onClick={() => void verifyAgain()}>
              {pending ? 'Doğrulanıyor…' : 'Kimliği yeniden doğrula'}
            </Button>
          </div>
        </div>
      ) : null}

      {!loading && view && !RUNNING.has(view.state) && !FINISHED.has(view.state) ? (
        <p>
          Sınav henüz başlamadı. <Link to={`/exams/${sessionId}/start`}>Sınav öncesi adımlara dön.</Link>
        </p>
      ) : null}

      {remaining !== null && remaining <= 0 && view && RUNNING.has(view.state) ? (
        <p>Süre doldu. Sınav sunucudaki cevaplarla teslim ediliyor.</p>
      ) : null}

      {question && view && ANSWERABLE.has(view.state) ? (
        <div className="exam-body">
          <div className="exam-question">
            <p>
              {index + 1}. {question.text}
            </p>
            <div className="option-list" role="radiogroup" aria-label={question.text}>
              {question.options.map((option, optionIndex) => (
                <label key={optionIndex} className="check">
                  <input
                    type="radio"
                    name={question.id}
                    checked={selections.get(question.id) === optionIndex}
                    disabled={!answering || pending}
                    onChange={() => choose(question.id, optionIndex)}
                  />
                  <span>{option}</span>
                </label>
              ))}
            </div>
          </div>
          <nav className="question-nav" aria-label="Soru gezinmesi">
            {questions.map((item, itemIndex) => {
              const answered = selections.get(item.id) != null
              return (
                <button
                  key={item.id}
                  type="button"
                  className={answered ? 'is-answered' : undefined}
                  aria-current={itemIndex === index ? 'true' : undefined}
                  onClick={() => setQuestionIndex(itemIndex)}
                >
                  {itemIndex + 1}
                </button>
              )
            })}
          </nav>
          <div className="action-row">
            <Button variant="secondary" disabled={index === 0} onClick={() => setQuestionIndex(index - 1)}>
              Önceki
            </Button>
            <Button variant="secondary" disabled={index >= questions.length - 1} onClick={() => setQuestionIndex(index + 1)}>
              Sonraki
            </Button>
          </div>
        </div>
      ) : null}

      {!loading && view && RUNNING.has(view.state) ? (
        <div className="action-row">
          <Button disabled={pending} onClick={() => setConfirming(true)}>
            Sınavı teslim et
          </Button>
        </div>
      ) : null}

      <Modal open={confirming} title="Sınavı teslim et" onClose={() => setConfirming(false)}>
        <p>Teslim ettikten sonra cevap değiştiremezsiniz.</p>
        <div className="action-row">
          <Button disabled={pending} onClick={() => void submit()}>
            {pending ? 'Teslim ediliyor…' : 'Teslim et'}
          </Button>
          <Button variant="secondary" disabled={pending} onClick={() => setConfirming(false)}>
            Vazgeç
          </Button>
        </div>
      </Modal>
    </section>
  )
}
