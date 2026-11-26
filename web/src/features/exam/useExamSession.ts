import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import { ApiError } from '../../api/client'
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
} from '../../api/sessions'
import { captureFrames } from '../camera/useCamera'
import { chosenOptions, enqueue, flushAnswers, listPending, nextSeq, removePending, type PendingAnswer } from './answerQueue'
import { remainingMs } from './remainingTime'
import { defineMessages, pick } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    savedLocally: 'Cevap bu cihaza kaydedildi. Bağlantı gelince gönderilecek.',
    saveFailed: 'Cevap kaydedilemedi.',
    notFound: 'Oturum bulunamadı.',
    loadFailed: 'Oturum alınamadı.',
    cameraFirst: 'Önce kamerayı açın.',
    verifyFailed: 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.',
    submitFailed: 'Sınav teslim edilemedi.',
  },
  en: {
    savedLocally: 'Your answer was saved on this device. It will be sent when the connection returns.',
    saveFailed: 'The answer could not be saved.',
    notFound: 'Session not found.',
    loadFailed: 'The session could not be loaded.',
    cameraFirst: 'Turn on the camera first.',
    verifyFailed: 'Your identity could not be verified. Make sure your face is clearly visible and try again.',
    submitFailed: 'The exam could not be submitted.',
  },
})

export const ANSWERABLE = new Set(['InProgress', 'Disconnected'])
export const RUNNING = new Set(['InProgress', 'PermissionPending', 'Disconnected'])
export const FINISHED = new Set(['Completed', 'Decided'])

/** Çevrimdışı bekleyen cevap bildirimi; geçerli dilde. */
export const savedLocally = () => pick(messages).savedLocally

function keptForLater(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504)
}

function messageOf(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error && error.message) return error.message
  return fallback
}

interface ClockAnchor {
  endsAt: string
  serverTime: string
}

/**
 * Sınav oturumunun bütün mantığı (S3, S4, E4 K-24): sunucudan yenileme, cevap kuyruğu (IndexedDB) ve
 * sıralı gönderim, çevrimdışı bekletme, kesintiden dönüş doğrulaması, teslim. Ekran bileşenleri yalnızca bunu çizer.
 */
export function useExamSession(sessionId: string, videoRef: RefObject<HTMLVideoElement | null>, cameraLive: boolean, stopCamera: () => void) {
  const lastSeq = useRef(0)
  const savedAnswers = useRef<SessionAnswer[]>([])
  const chain = useRef(Promise.resolve())
  const offline = useRef(false)
  const sending = useRef(new Set<number>())
  const [loading, setLoading] = useState(true)
  const [view, setView] = useState<SessionView | null>(null)
  const [questions, setQuestions] = useState<SessionQuestion[]>([])
  const [selections, setSelections] = useState<Map<string, number | null>>(new Map())
  const [queued, setQueued] = useState(0)
  const [anchor, setAnchor] = useState<ClockAnchor | null>(null)
  const [elapsed, setElapsed] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [connected, setConnected] = useState(true)
  const [agent, setAgent] = useState<AgentStatus | null>(null)
  const [lastSavedAt, setLastSavedAt] = useState<Date | null>(null)

  // Cevaplar sırayla işlenir: iki hızlı seçim aynı seq'i almasın.
  const exclusive = useCallback(<T>(task: () => Promise<T>): Promise<T> => {
    const run = chain.current.then(task, task)
    chain.current = run.then(
      () => undefined,
      () => undefined,
    )
    return run
  }, [])

  const refreshSelections = useCallback(async () => {
    const pendingAnswers = await listPending(sessionId)
    setQueued(pendingAnswers.filter((item) => !sending.current.has(item.seq)).length)
    setSelections(chosenOptions(savedAnswers.current, pendingAnswers))
  }, [sessionId])

  const deliver = useCallback(async (answer: PendingAnswer): Promise<'sent' | 'drop' | 'stop'> => {
    try {
      await saveAnswer(answer.sessionId, {
        questionId: answer.questionId,
        selectedOption: answer.selectedOption,
        seq: answer.seq,
        clientTime: answer.clientTime,
      })
      await removePending(answer)
      lastSeq.current = Math.max(lastSeq.current, answer.seq)
      savedAnswers.current = [...savedAnswers.current, { questionId: answer.questionId, selectedOption: answer.selectedOption, seq: answer.seq }]
      setLastSavedAt(new Date())
      return 'sent'
    } catch (caught) {
      if (keptForLater(caught)) {
        offline.current = true
        return 'stop'
      }
      setError(messageOf(caught, pick(messages).saveFailed))
      await removePending(answer)
      return 'drop'
    }
  }, [])

  const flush = useCallback(async (): Promise<boolean> => {
    const { stopped } = await flushAnswers(await listPending(sessionId), deliver)
    await refreshSelections()
    if (stopped && offline.current) setNotice(savedLocally())
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
      await refreshSelections()
    },
    [refreshSelections],
  )

  const pull = useCallback(async () => {
    try {
      let next = await getSession(sessionId)
      if (!next) {
        setError(pick(messages).notFound)
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
        setNotice(savedLocally())
        return
      }
      setError(messageOf(caught, pick(messages).loadFailed))
    } finally {
      setLoading(false)
    }
  }, [apply, flush, sessionId])

  // En geç 10 sn'de bir yenileme: 30 sn istek gelmezse oturum kesik sayılıyor (K-24). Süre sunucu saatine göre.
  useEffect(() => {
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
  }, [pull])

  /** `selectedOption: null` seçimi temizler (K-24). */
  const choose = (questionId: string, selectedOption: number | null) => {
    if (!view || !ANSWERABLE.has(view.state)) return
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
        await refreshSelections()
        const sent = await deliver(answer)
        if (sent === 'stop' && offline.current) setNotice(savedLocally())
      } finally {
        sending.current.delete(answer.seq)
        await refreshSelections()
      }
    })
  }

  /** Kesintiden dönüş: önce kuyruk, sonra yeniden doğrulama (FR-26). Başarısızlık 422 ile gelir. */
  const verifyAgain = async () => {
    const video = videoRef.current
    if (!cameraLive || !video) {
      setError(pick(messages).cameraFirst)
      return
    }
    setPending(true)
    setError(null)
    try {
      // Kesik oturumda cevaplar yeniden doğrulamadan önce gider; sunucu onları izlenmeyen diye işaretler.
      if (!(await flush())) return
      const frames = await captureFrames(video)
      await verifyIdentity(sessionId, frames)
      stopCamera()
      await pull()
    } catch (caught) {
      setError(messageOf(caught, pick(messages).verifyFailed))
    } finally {
      setPending(false)
    }
  }

  /** Teslimden önce kuyruk gönderilir; gönderilemezse teslim edilmez. */
  const submit = async (): Promise<boolean> => {
    setPending(true)
    setError(null)
    try {
      if (!(await flush())) return false
      await submitExam(sessionId)
      await pull()
      return true
    } catch (caught) {
      setError(messageOf(caught, pick(messages).submitFailed))
      return false
    } finally {
      setPending(false)
    }
  }

  const remaining = anchor ? remainingMs(anchor.endsAt, anchor.serverTime, elapsed) : null

  return {
    loading,
    view,
    questions,
    selections,
    queued,
    remaining,
    error,
    notice,
    pending,
    connected,
    agent,
    lastSavedAt,
    pull,
    choose,
    verifyAgain,
    submit,
  }
}

export type ExamSession = ReturnType<typeof useExamSession>
