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
import './IdentityStep.css'

const TIPS = [
  { title: 'Yüzünüzü ovalin içine ortalayın', text: 'Kameraya düz bakın, başınızı sabit tutun.' },
  { title: 'Işığı önünüze alın', text: 'Arkanızda pencere ya da parlak lamba varsa yüzünüz gölgede kalır.' },
  { title: 'Yüzünüzü açık bırakın', text: 'Koyu gözlük, şapka ve maskeyi çıkarın.' },
  { title: 'Karede yalnızca siz olun', text: 'Arkada başka biri ya da bir fotoğraf varsa doğrulama başarısız olur.' },
] as const

type Failure = { title?: string; text: string }

function failureOf(error: unknown): Failure {
  if (error instanceof ApiError) {
    // 422 canlı yüz yok / eşleşmedi, 429 çok sık deneme, 503 görüntü servisi yok: neden "title"da, ne yapılacağı "detail"de.
    return { title: error.title !== error.message ? error.title : undefined, text: error.message }
  }
  if (error instanceof Error && error.message) return { text: error.message }
  return { text: 'Kimlik doğrulanamadı. Yüzünüzün net göründüğünden emin olup yeniden deneyin.' }
}

/**
 * Biyometrik kimlik doğrulama (Stitch ekran 12; `POST /api/sessions/{id}/verify`, A3, K-27).
 * Başarılı doğrulama sınavı başlatır (K-24); aday doğrudan sınav ekranına gider.
 */
export function IdentityStep({ sessionId }: { sessionId: string }) {
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
        <h2 id="identity-step-title">Kimlik doğrulama</h2>
        <p>Yüzünüz kayıt fotoğrafınızla karşılaştırılır. Doğrulama başarılı olunca sınav başlar.</p>
      </header>

      <div className="identity-layout">
        <div className="identity-camera">
          <CameraView
            videoRef={videoRef}
            live={camera.live}
            label="Kimlik doğrulama kamerası"
            placeholder={
              <>
                <p>Kamera yalnızca siz açınca çalışır. Görüntü kaydedilmez; yalnızca doğrulama için birkaç kare gönderilir.</p>
                <Button onClick={() => void camera.start()} icon={<Camera size={18} aria-hidden="true" />}>
                  Kamerayı aç
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
              {pending ? 'Doğrulanıyor…' : 'Yüzümü doğrula ve sınavı başlat'}
            </Button>
            {failure ? (
              <Button variant="secondary" onClick={() => setSupportOpen(true)} icon={<LifeBuoy size={18} aria-hidden="true" />}>
                Destek talep et
              </Button>
            ) : null}
          </div>
        </div>

        <aside className="identity-tips" aria-labelledby="identity-tips-title">
          <h3 id="identity-tips-title">Başarılı doğrulama için</h3>
          <ol>
            {TIPS.map((tip) => (
              <li key={tip.title}>
                <strong>{tip.title}</strong>
                <span>{tip.text}</span>
              </li>
            ))}
          </ol>
        </aside>
      </div>

      <Modal open={supportOpen} title="Destek talep et" onClose={() => setSupportOpen(false)}>
        <p>Doğrulama tamamlanamadı. Sınav sorumlusuna aşağıdaki oturum numarasını iletin; numara kişisel bilgi içermez.</p>
        <p className="identity-session-id numeric">{sessionId}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setSupportOpen(false)}>
            Kapat
          </Button>
        </div>
      </Modal>
    </section>
  )
}
