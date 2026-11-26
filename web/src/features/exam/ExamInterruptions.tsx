import { ArrowLeft, Camera, CircleCheckBig, ScanFace, TimerOff, VideoOff, WifiOff } from 'lucide-react'
import type { RefObject } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { defineMessages, useMessages } from '../../i18n/messages'
import { CameraView } from '../camera/CameraView'

const messages = defineMessages({
  tr: {
    offline: 'Bağlantınız koptu.',
    offlineTail: 'Süre sunucuda işlemeye devam ediyor.',
    queued: (n: number) => ` Bekleyen cevap: ${n}.`,
    permissionTitle: 'Kamera ve mikrofon izni gerekli',
    permissionText: 'Kamera ve mikrofon izni geri alındığı için sorular gizlendi. Süre işlemeye devam ediyor. İzin verince sorular yeniden açılır.',
    permissionHint: 'İzinsiz geçen süre kaydedilir ve değerlendiriciye gösterilir.',
    waiting: 'İzin bekleniyor…',
    allow: 'Kamera ve mikrofona izin ver',
    welcomeBack: 'Tekrar hoş geldiniz',
    suspended: 'Bağlantı kesildiği için oturum askıya alındı; süre sunucuda işlemeye devam etti.',
    willSend: 'Cihazınızdaki cevaplar doğrulamadan önce gönderilecek.',
    sent: 'Kayıtlı cevaplarınız gönderildi.',
    verifyToReturn: 'Sınava dönmek için kimliğinizi yeniden doğrulayın.',
    cameraLabel: 'Yeniden doğrulama kamerası',
    openCamera: 'Kamerayı aç',
    verifying: 'Doğrulanıyor…',
    verifyAgain: 'Kimliği yeniden doğrula',
    timeUp: 'Sınav süresi doldu',
    timeUpText: 'Sınav, süre dolana kadar kaydedilen cevaplarınızla otomatik teslim ediliyor. Bu sayfa birkaç saniye içinde güncellenir.',
    finished: 'Sınav teslim edildi.',
    answered: (answered: number, total: number) => `${total} sorudan ${answered} tanesini cevapladınız. `,
    finishedText: 'Kamera ve mikrofon kapatıldı. Bu sayfayı kapatabilirsiniz.',
    backToExams: 'Sınavlarıma dön',
    notStarted: 'Sınav henüz başlamadı',
    notStartedText: 'Sınav öncesi adımları tamamladıktan sonra sorular açılır.',
    backToSteps: 'Sınav öncesi adımlara dön',
  },
  en: {
    offline: 'Your connection dropped.',
    offlineTail: 'The timer keeps running on the server.',
    queued: (n: number) => ` Answers waiting: ${n}.`,
    permissionTitle: 'Camera and microphone permission needed',
    permissionText: 'The questions are hidden because camera and microphone permission was revoked. The timer keeps running. The questions return once you allow access.',
    permissionHint: 'Time without permission is recorded and shown to the reviewer.',
    waiting: 'Waiting for permission…',
    allow: 'Allow camera and microphone',
    welcomeBack: 'Welcome back',
    suspended: 'The session was paused because the connection dropped; the timer kept running on the server.',
    willSend: 'Answers on your device will be sent before verification.',
    sent: 'Your saved answers have been sent.',
    verifyToReturn: 'Verify your identity again to return to the exam.',
    cameraLabel: 'Re-verification camera',
    openCamera: 'Turn on camera',
    verifying: 'Verifying…',
    verifyAgain: 'Verify identity again',
    timeUp: 'Time is up',
    timeUpText: 'The exam is being submitted automatically with the answers saved until time ran out. This page updates in a few seconds.',
    finished: 'Exam submitted.',
    answered: (answered: number, total: number) => `You answered ${answered} of ${total} questions. `,
    finishedText: 'The camera and microphone were turned off. You can close this page.',
    backToExams: 'Back to my exams',
    notStarted: 'The exam has not started yet',
    notStartedText: 'The questions open after you complete the pre-exam steps.',
    backToSteps: 'Back to pre-exam steps',
  },
})

/** Çevrimdışı: aday cevaplamaya devam eder, cevaplar cihazda (IndexedDB) bekler (Stitch ekran 15). */
export function OfflineBanner({ message, queued }: { message: string; queued: number }) {
  const m = useMessages(messages)
  return (
    <div className="exam-banner exam-banner-offline" role="status">
      <WifiOff size={20} aria-hidden="true" />
      <p>
        <strong>{m.offline}</strong> {message} {m.offlineTail}
        {queued > 0 ? <span className="numeric">{m.queued(queued)}</span> : null}
      </p>
    </div>
  )
}

interface PermissionPanelProps {
  error: string | null
  requesting: boolean
  onRequest: () => void
}

/** İzin kaldırıldı (S4, FR-17): sorular gizli, süre işliyor; izin verilince sorular döner. */
export function PermissionPanel({ error, requesting, onRequest }: PermissionPanelProps) {
  const m = useMessages(messages)
  return (
    <section className="exam-state" aria-labelledby="permission-title">
      <span className="exam-state-icon is-warning" aria-hidden="true">
        <VideoOff size={28} />
      </span>
      <h2 id="permission-title">{m.permissionTitle}</h2>
      <p>{m.permissionText}</p>
      <p className="exam-state-hint">{m.permissionHint}</p>
      {error ? <Alert tone="danger">{error}</Alert> : null}
      <Button size="lg" loading={requesting} onClick={onRequest} icon={requesting ? undefined : <Camera size={18} aria-hidden="true" />}>
        {requesting ? m.waiting : m.allow}
      </Button>
    </section>
  )
}

interface ReconnectPanelProps {
  videoRef: RefObject<HTMLVideoElement | null>
  cameraLive: boolean
  cameraError: string | null
  queued: number
  pending: boolean
  onOpenCamera: () => void
  onVerify: () => void
}

/** Sunucu oturumu kesik saydı (K-24): dönmek için yeniden kimlik doğrulama (FR-26). */
export function ReconnectPanel({ videoRef, cameraLive, cameraError, queued, pending, onOpenCamera, onVerify }: ReconnectPanelProps) {
  const m = useMessages(messages)
  return (
    <section className="exam-state exam-state-wide" aria-labelledby="reconnect-title">
      <h2 id="reconnect-title">{m.welcomeBack}</h2>
      <p>
        {m.suspended} {queued > 0 ? m.willSend : m.sent} {m.verifyToReturn}
      </p>
      <div className="exam-reconnect-camera">
        <CameraView
          videoRef={videoRef}
          live={cameraLive}
          label={m.cameraLabel}
          placeholder={
            <Button onClick={onOpenCamera} icon={<Camera size={18} aria-hidden="true" />}>
              {m.openCamera}
            </Button>
          }
        />
      </div>
      {cameraError ? <Alert tone="danger">{cameraError}</Alert> : null}
      <Button size="lg" loading={pending} disabled={!cameraLive} onClick={onVerify} icon={pending ? undefined : <ScanFace size={20} aria-hidden="true" />}>
        {pending ? m.verifying : m.verifyAgain}
      </Button>
    </section>
  )
}

/** Süre doldu: sunucu oturumu kendisi teslim eder (K-24 otomatik teslim). */
export function TimeUpPanel() {
  const m = useMessages(messages)
  return (
    <section className="exam-state" aria-labelledby="timeup-title" role="status">
      <span className="exam-state-icon is-warning" aria-hidden="true">
        <TimerOff size={28} />
      </span>
      <h2 id="timeup-title">{m.timeUp}</h2>
      <p>{m.timeUpText}</p>
    </section>
  )
}

/** Teslim edildi (Stitch ekran 16'nın sade hâli): sonuç ve karar adaya gösterilmez. */
export function FinishedPanel({ answered, total }: { answered: number; total: number }) {
  const m = useMessages(messages)
  return (
    <section className="exam-state" aria-labelledby="finished-title">
      <span className="exam-state-icon is-success" aria-hidden="true">
        <CircleCheckBig size={28} />
      </span>
      <h2 id="finished-title">{m.finished}</h2>
      <p>
        {total > 0 ? (
          <span className="numeric">{m.answered(answered, total)}</span>
        ) : null}
        {m.finishedText}
      </p>
      <Link className="button button-secondary button-md" to="/exams">
        <ArrowLeft size={18} aria-hidden="true" />
        {m.backToExams}
      </Link>
    </section>
  )
}

/** Oturum henüz başlamadı: sihirbaza dön. */
export function NotStartedPanel({ sessionId }: { sessionId: string }) {
  const m = useMessages(messages)
  return (
    <section className="exam-state" aria-labelledby="notstarted-title">
      <h2 id="notstarted-title">{m.notStarted}</h2>
      <p>{m.notStartedText}</p>
      <Link className="button button-primary button-md" to={`/exams/${sessionId}/start`}>
        {m.backToSteps}
      </Link>
    </section>
  )
}
