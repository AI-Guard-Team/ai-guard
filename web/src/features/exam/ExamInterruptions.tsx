import { ArrowLeft, Camera, CircleCheckBig, ScanFace, TimerOff, VideoOff, WifiOff } from 'lucide-react'
import type { RefObject } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { CameraView } from '../camera/CameraView'

/** Çevrimdışı: aday cevaplamaya devam eder, cevaplar cihazda (IndexedDB) bekler (Stitch ekran 15). */
export function OfflineBanner({ message, queued }: { message: string; queued: number }) {
  return (
    <div className="exam-banner exam-banner-offline" role="status">
      <WifiOff size={20} aria-hidden="true" />
      <p>
        <strong>Bağlantınız koptu.</strong> {message} Süre sunucuda işlemeye devam ediyor.
        {queued > 0 ? <span className="numeric"> Bekleyen cevap: {queued}.</span> : null}
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
  return (
    <section className="exam-state" aria-labelledby="permission-title">
      <span className="exam-state-icon is-warning" aria-hidden="true">
        <VideoOff size={28} />
      </span>
      <h2 id="permission-title">Kamera ve mikrofon izni gerekli</h2>
      <p>Kamera ve mikrofon izni geri alındığı için sorular gizlendi. Süre işlemeye devam ediyor. İzin verince sorular yeniden açılır.</p>
      <p className="exam-state-hint">İzinsiz geçen süre kaydedilir ve değerlendiriciye gösterilir.</p>
      {error ? <Alert tone="danger">{error}</Alert> : null}
      <Button size="lg" loading={requesting} onClick={onRequest} icon={requesting ? undefined : <Camera size={18} aria-hidden="true" />}>
        {requesting ? 'İzin bekleniyor…' : 'Kamera ve mikrofona izin ver'}
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
  return (
    <section className="exam-state exam-state-wide" aria-labelledby="reconnect-title">
      <h2 id="reconnect-title">Tekrar hoş geldiniz</h2>
      <p>
        Bağlantı kesildiği için oturum askıya alındı; süre sunucuda işlemeye devam etti.{' '}
        {queued > 0 ? 'Cihazınızdaki cevaplar doğrulamadan önce gönderilecek.' : 'Kayıtlı cevaplarınız gönderildi.'} Sınava dönmek için kimliğinizi yeniden
        doğrulayın.
      </p>
      <div className="exam-reconnect-camera">
        <CameraView
          videoRef={videoRef}
          live={cameraLive}
          label="Yeniden doğrulama kamerası"
          placeholder={
            <Button onClick={onOpenCamera} icon={<Camera size={18} aria-hidden="true" />}>
              Kamerayı aç
            </Button>
          }
        />
      </div>
      {cameraError ? <Alert tone="danger">{cameraError}</Alert> : null}
      <Button size="lg" loading={pending} disabled={!cameraLive} onClick={onVerify} icon={pending ? undefined : <ScanFace size={20} aria-hidden="true" />}>
        {pending ? 'Doğrulanıyor…' : 'Kimliği yeniden doğrula'}
      </Button>
    </section>
  )
}

/** Süre doldu: sunucu oturumu kendisi teslim eder (K-24 otomatik teslim). */
export function TimeUpPanel() {
  return (
    <section className="exam-state" aria-labelledby="timeup-title" role="status">
      <span className="exam-state-icon is-warning" aria-hidden="true">
        <TimerOff size={28} />
      </span>
      <h2 id="timeup-title">Sınav süresi doldu</h2>
      <p>Sınav, süre dolana kadar kaydedilen cevaplarınızla otomatik teslim ediliyor. Bu sayfa birkaç saniye içinde güncellenir.</p>
    </section>
  )
}

/** Teslim edildi (Stitch ekran 16'nın sade hâli): sonuç ve karar adaya gösterilmez. */
export function FinishedPanel({ answered, total }: { answered: number; total: number }) {
  return (
    <section className="exam-state" aria-labelledby="finished-title">
      <span className="exam-state-icon is-success" aria-hidden="true">
        <CircleCheckBig size={28} />
      </span>
      <h2 id="finished-title">Sınav teslim edildi.</h2>
      <p>
        {total > 0 ? (
          <span className="numeric">
            {total} sorudan {answered} tanesini cevapladınız.{' '}
          </span>
        ) : null}
        Kamera ve mikrofon kapatıldı. Bu sayfayı kapatabilirsiniz.
      </p>
      <Link className="button button-secondary button-md" to="/exams">
        <ArrowLeft size={18} aria-hidden="true" />
        Sınavlarıma dön
      </Link>
    </section>
  )
}

/** Oturum henüz başlamadı: sihirbaza dön. */
export function NotStartedPanel({ sessionId }: { sessionId: string }) {
  return (
    <section className="exam-state" aria-labelledby="notstarted-title">
      <h2 id="notstarted-title">Sınav henüz başlamadı</h2>
      <p>Sınav öncesi adımları tamamladıktan sonra sorular açılır.</p>
      <Link className="button button-primary button-md" to={`/exams/${sessionId}/start`}>
        Sınav öncesi adımlara dön
      </Link>
    </section>
  )
}
