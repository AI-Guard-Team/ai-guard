import { ArrowLeft, ArrowRight, Maximize, Send } from 'lucide-react'
import { useRef, useState } from 'react'
import { useParams } from 'react-router'
import { Alert } from '../../components/Alert'
import { BrandMark } from '../../components/BrandMark'
import { Button } from '../../components/Button'
import { Skeleton } from '../../components/Skeleton'
import { useCamera } from '../camera/useCamera'
import { agentChip, cameraChip, connectionChip } from './examChips'
import { FinishedPanel, NotStartedPanel, OfflineBanner, PermissionPanel, ReconnectPanel, TimeUpPanel } from './ExamInterruptions'
import { ExamStatusChips } from './ExamStatusChips'
import { ExamTimer } from './ExamTimer'
import { QuestionCard } from './QuestionCard'
import { QuestionNavigator } from './QuestionNavigator'
import { SubmitDialog } from './SubmitDialog'
import { useCameraPermission, useFullscreen } from './useBrowserState'
import { useExamMonitoring } from './useExamMonitoring'
import { ANSWERABLE, FINISHED, RUNNING, useExamSession } from './useExamSession'
import './ExamPage.css'

/**
 * Canlı sınav ekranı (Stitch ekran 14) ve kesinti durumları (15). Yan menü yok: aday yalnızca sınava odaklanır.
 * Mantık `useExamSession` ve `useExamMonitoring`'de; bu bileşen durumlara göre çizer.
 */
export function ExamPage() {
  const { sessionId = '' } = useParams()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const exam = useExamSession(sessionId, videoRef, camera.live, camera.stop)
  const monitoring = useExamMonitoring({ sessionId, state: exam.view?.state, onStateChange: () => void exam.pull() })
  const permission = useCameraPermission()
  const fullscreen = useFullscreen()
  const [questionIndex, setQuestionIndex] = useState(0)
  const [marked, setMarked] = useState<ReadonlySet<string>>(new Set())
  const [confirming, setConfirming] = useState(false)

  const { view, questions, selections, remaining } = exam
  const state = view?.state
  const timeUp = remaining !== null && remaining <= 0 && state !== undefined && RUNNING.has(state)
  const answering = state !== undefined && ANSWERABLE.has(state) && !timeUp
  const index = questions.length === 0 ? 0 : Math.min(questionIndex, questions.length - 1)
  const question = questions[index]
  const answered = questions.filter((item) => selections.get(item.id) != null).length

  const toggleMark = (id: string) =>
    setMarked((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const submit = async () => {
    if (await exam.submit()) setConfirming(false)
  }

  const chips = [cameraChip(state, camera.live || monitoring.live, permission), agentChip(exam.agent), connectionChip(exam.connected, state)]

  return (
    <div className="exam-room">
      <header className="exam-topbar">
        <div className="exam-topbar-title">
          <span className="exam-topbar-brand">
            <BrandMark />
          </span>
          {view ? <h1>{view.examName}</h1> : <Skeleton height="1.5rem" width="12rem" />}
        </div>
        <ExamTimer remaining={remaining} />
        <div className="exam-topbar-actions">
          <ExamStatusChips chips={chips} />
          {state === 'InProgress' && !fullscreen.fullscreen && fullscreen.supported ? (
            <Button variant="secondary" onClick={fullscreen.enter} icon={<Maximize size={18} aria-hidden="true" />}>
              Tam ekrana geç
            </Button>
          ) : null}
          {/* Düğme her zaman yer kaplar; sınav sürmüyorsa görünmez ve tıklanamaz (yüklemede çubuk uzamasın). */}
          {state !== undefined && RUNNING.has(state) ? (
            <Button disabled={exam.pending} onClick={() => setConfirming(true)} icon={<Send size={18} aria-hidden="true" />}>
              Sınavı teslim et
            </Button>
          ) : (
            <span className="exam-topbar-placeholder" aria-hidden="true">
              <Button tabIndex={-1} disabled icon={<Send size={18} />}>
                Sınavı teslim et
              </Button>
            </span>
          )}
        </div>
      </header>

      {exam.notice ? <OfflineBanner message={exam.notice} queued={exam.queued} /> : null}

      <main className="exam-main" id="main">
        {exam.error && !confirming ? <Alert tone="danger">{exam.error}</Alert> : null}
        {/* S4: kamera/mikrofon açılamadıysa ya da tarayıcı desteklemiyorsa. */}
        {state === 'InProgress' && monitoring.error ? <Alert tone="warning">{monitoring.error}</Alert> : null}

        {exam.loading ? (
          <div className="exam-layout" aria-busy="true">
            <Skeleton height="26rem" radius="lg" />
            <Skeleton height="16rem" radius="lg" />
          </div>
        ) : !view ? null : FINISHED.has(view.state) ? (
          <FinishedPanel answered={answered} total={questions.length} />
        ) : !RUNNING.has(view.state) ? (
          <NotStartedPanel sessionId={sessionId} />
        ) : timeUp ? (
          <TimeUpPanel />
        ) : view.state === 'PermissionPending' ? (
          <PermissionPanel error={monitoring.error} requesting={monitoring.requesting} onRequest={() => void monitoring.requestPermission()} />
        ) : (
          <>
            {view.state === 'Disconnected' ? (
              <ReconnectPanel
                videoRef={videoRef}
                cameraLive={camera.live}
                cameraError={camera.error}
                queued={exam.queued}
                pending={exam.pending}
                onOpenCamera={() => void camera.start()}
                onVerify={() => void exam.verifyAgain()}
              />
            ) : null}
            {question ? (
              <div className="exam-layout">
                <div className="exam-question-column">
                  <QuestionCard
                    question={question}
                    index={index}
                    total={questions.length}
                    selected={selections.get(question.id)}
                    disabled={!answering || exam.pending}
                    marked={marked.has(question.id)}
                    queued={exam.queued}
                    lastSavedAt={exam.lastSavedAt}
                    onChoose={(option) => exam.choose(question.id, option)}
                    onToggleMark={() => toggleMark(question.id)}
                  />
                  <div className="exam-pager">
                    <Button variant="secondary" disabled={index === 0} onClick={() => setQuestionIndex(index - 1)} icon={<ArrowLeft size={18} aria-hidden="true" />}>
                      Önceki
                    </Button>
                    <Button
                      variant="secondary"
                      disabled={index >= questions.length - 1}
                      onClick={() => setQuestionIndex(index + 1)}
                      iconEnd={<ArrowRight size={18} aria-hidden="true" />}
                    >
                      Sonraki
                    </Button>
                  </div>
                </div>
                <QuestionNavigator questions={questions} current={index} selections={selections} marked={marked} onSelect={setQuestionIndex} />
              </div>
            ) : null}
          </>
        )}
      </main>

      <SubmitDialog
        open={confirming}
        total={questions.length}
        answered={answered}
        marked={marked.size}
        queued={exam.queued}
        pending={exam.pending}
        error={confirming ? exam.error : null}
        onConfirm={() => void submit()}
        onClose={() => setConfirming(false)}
      />
    </div>
  )
}
