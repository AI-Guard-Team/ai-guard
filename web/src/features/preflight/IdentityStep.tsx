import { Camera, LifeBuoy, ScanFace } from 'lucide-react'
import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { ApiError } from '../../api/client'
import { verifyIdentity } from '../../api/sessions'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Modal } from '../../components/Modal'
import { CameraView } from '../camera/CameraView'
import { captureFrames, useCamera } from '../camera/useCamera'
import { common } from '../../i18n/common'
import { defineMessages, pick, useMessages } from '../../i18n/messages'
import './IdentityStep.css'

const messages = defineMessages({
  tr: {
    tips: [
      { title: 'Yüzünüzü ovalin içine ortalayın', text: 'Kameraya düz bakın, başınızı sabit tutun.' },
      { title: 'Işığı önünüze alın', text: 'Arkanızda pencere ya da parlak lamba varsa yüzünüz gölgede kalır.' },
      { title: 'Yüzünüzü açık bırakın', text: 'Koyu gözlük, şapka ve maskeyi çıkarın.' },
      { title: 'Karede yalnızca siz olun', text: 'Arkada başka biri ya da bir fotoğraf varsa doğrulama başarısız olur.' },
    ],
    failed: 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.',
    notMatched: 'Yüzünüz doğrulanamadı. Işığı ve konumunuzu düzeltip yeniden deneyin.',
    serviceDown: 'Görüntü servisi şu an yanıt vermiyor. Sorun sizde değil; birkaç dakika sonra yeniden deneyin. Süreniz henüz başlamadı.',
    title: 'Kimlik doğrulama',
    text: 'Yüzünüz kayıt fotoğrafınızla karşılaştırılır. Doğrulama başarılı olunca sınav başlar.',
    cameraLabel: 'Kimlik doğrulama kamerası',
    cameraText: 'Kamera yalnızca siz açınca çalışır. Görüntü kaydedilmez; yalnızca doğrulama için birkaç kare gönderilir.',
    openCamera: 'Kamerayı aç',
    verifying: 'Doğrulanıyor…',
    verify: 'Yüzümü doğrula ve sınavı başlat',
    support: 'Destek talep et',
    tipsTitle: 'Başarılı doğrulama için',
    supportText: 'Doğrulama tamamlanamadı. Sınav sorumlusuna aşağıdaki oturum numarasını iletin; numara kişisel bilgi içermez.',
  },
  en: {
    tips: [
      { title: 'Center your face in the oval', text: 'Look straight at the camera and keep your head still.' },
      { title: 'Light from the front', text: 'A window or bright lamp behind you leaves your face in shadow.' },
      { title: 'Keep your face uncovered', text: 'Remove dark glasses, hats and masks.' },
      { title: 'Only you in the frame', text: 'Verification fails if someone else or a photo is in the background.' },
    ],
    failed: 'Your identity could not be verified. Make sure your face is clearly visible and try again.',
    notMatched: 'Your face could not be verified. Adjust the light and your position, then try again.',
    serviceDown: 'The image service is not responding right now. The problem is not on your side; try again in a few minutes. Your time has not started yet.',
    title: 'Identity check',
    text: 'Your face is compared with your reference photo. The exam starts when verification succeeds.',
    cameraLabel: 'Identity check camera',
    cameraText: 'The camera only runs when you turn it on. Nothing is recorded; only a few frames are sent for verification.',
    openCamera: 'Turn on camera',
    verifying: 'Verifying…',
    verify: 'Verify my face and start the exam',
    support: 'Request support',
    tipsTitle: 'For a successful check',
    supportText: 'Verification could not be completed. Give the exam coordinator the session number below; it contains no personal information.',
  },
})

type Failure = { title?: string; text: string }

function failureOf(error: unknown): Failure {
  if (error instanceof ApiError) {
    // 422 canlı yüz yok / eşleşmedi, 429 çok sık deneme, 503 görüntü servisi yok: neden "title"da, ne yapılacağı "detail"de.
    if (error.status === 503) return { text: pick(messages).serviceDown }
    if (error.status === 422 && !error.title) return { text: pick(messages).notMatched }
    return { title: error.title !== error.message ? error.title : undefined, text: error.message }
  }
  if (error instanceof Error && error.message) return { text: error.message }
  return { text: pick(messages).failed }
}

/**
 * Biyometrik kimlik doğrulama (Stitch ekran 12; `POST /api/sessions/{id}/verify`, A3, K-27).
 * Başarılı doğrulama sınavı başlatır (K-24); aday doğrudan sınav ekranına gider.
 */
export function IdentityStep({ sessionId }: { sessionId: string }) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const navigate = useNavigate()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const [pending, setPending] = useState(false)
  const [failure, setFailure] = useState<Failure | null>(null)
  const [supportOpen, setSupportOpen] = useState(false)

  const verify = async () => {
    const video = videoRef.current
    if (!camera.live || !video) return
    setPending(true)
    setFailure(null)
    try {
      const frames = await captureFrames(video)
      await verifyIdentity(sessionId, frames)
      camera.stop()
      navigate(`/exams/${sessionId}`, { replace: true })
    } catch (caught) {
      setFailure(failureOf(caught))
      setPending(false)
    }
  }

  return (
    <section className="step-panel" aria-labelledby="identity-step-title">
      <header>
        <h2 id="identity-step-title">{m.title}</h2>
        <p>{m.text}</p>
      </header>

      <div className="identity-layout">
        <div className="identity-camera">
          <CameraView
            videoRef={videoRef}
            live={camera.live}
            label={m.cameraLabel}
            placeholder={
              <>
                <p>{m.cameraText}</p>
                <Button onClick={() => void camera.start()} icon={<Camera size={18} aria-hidden="true" />}>
                  {m.openCamera}
                </Button>
              </>
            }
          />
          {camera.error ? <Alert tone="danger">{camera.error}</Alert> : null}
          {failure ? (
            <Alert tone="danger" title={failure.title}>
              {failure.text}
            </Alert>
          ) : null}
          <div className="identity-actions">
            <Button size="lg" loading={pending} disabled={!camera.live} onClick={() => void verify()} icon={pending ? undefined : <ScanFace size={20} aria-hidden="true" />}>
              {pending ? m.verifying : m.verify}
            </Button>
            {failure ? (
              <Button variant="secondary" onClick={() => setSupportOpen(true)} icon={<LifeBuoy size={18} aria-hidden="true" />}>
                {m.support}
              </Button>
            ) : null}
          </div>
        </div>

        <aside className="identity-tips" aria-labelledby="identity-tips-title">
          <h3 id="identity-tips-title">{m.tipsTitle}</h3>
          <ol>
            {m.tips.map((tip) => (
              <li key={tip.title}>
                <strong>{tip.title}</strong>
                <span>{tip.text}</span>
              </li>
            ))}
          </ol>
        </aside>
      </div>

      <Modal open={supportOpen} title={m.support} onClose={() => setSupportOpen(false)}>
        <p>{m.supportText}</p>
        <p className="identity-session-id numeric">{sessionId}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setSupportOpen(false)}>
            {c.close}
          </Button>
        </div>
      </Modal>
    </section>
  )
}
