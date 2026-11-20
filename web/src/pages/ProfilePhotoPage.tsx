import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { uploadReferencePhoto, withdrawBiometricConsent } from '../api/profile'
import { Button } from '../components/Button'
import { FormAlert } from '../components/AuthCard'
import { Modal } from '../components/Modal'
import { useToast } from '../components/useToast'
import { useCamera } from './useCamera'

const MAX_BYTES = 5 * 1024 * 1024
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

function photoError(file: File): string | null {
  if (!ACCEPTED_TYPES.includes(file.type)) return 'Fotoğraf JPEG, PNG veya WebP olmalıdır.'
  if (file.size > MAX_BYTES) return 'Fotoğraf 5 MB’den küçük olmalıdır.'
  return null
}

export function ProfilePhotoPage() {
  const showToast = useToast()
  const videoRef = useRef<HTMLVideoElement>(null)
  const camera = useCamera(videoRef)
  const [consent, setConsent] = useState(false)
  const [photo, setPhoto] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [withdrawOpen, setWithdrawOpen] = useState(false)
  const [withdrawError, setWithdrawError] = useState<string | null>(null)
  const [withdrawing, setWithdrawing] = useState(false)

  const previewUrl = useMemo(() => (photo ? URL.createObjectURL(photo) : null), [photo])
  useEffect(() => {
    if (!previewUrl) return
    return () => URL.revokeObjectURL(previewUrl)
  }, [previewUrl])

  const remember = (file: File) => {
    const problem = photoError(file)
    if (problem) {
      setError(problem)
      return
    }
    setError(null)
    setPhoto(file)
  }

  const openCamera = () => {
    if (!consent) return
    void camera.start()
  }

  const takePhoto = () => {
    const video = videoRef.current
    if (!video) return
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth || 640
    canvas.height = video.videoHeight || 480
    const context = canvas.getContext('2d')
    if (!context) {
      setError('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')
      return
    }
    context.drawImage(video, 0, 0, canvas.width, canvas.height)
    canvas.toBlob((blob) => {
      if (!blob) {
        setError('Kare alınamadı. Kamerayı yeniden açıp tekrar deneyin.')
        return
      }
      remember(new File([blob], 'kayit-fotografi.jpg', { type: 'image/jpeg' }))
      camera.stop()
    }, 'image/jpeg', 0.92)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (!consent) {
      setError('Biyometrik rıza olmadan fotoğraf kaydedilemez.')
      return
    }
    if (!photo) {
      setError('Bir fotoğraf seçin veya kameradan çekin.')
      return
    }
    setPending(true)
    setError(null)
    try {
      await uploadReferencePhoto(photo)
      showToast('Kayıt fotoğrafı kaydedildi.', 'success')
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Fotoğraf kaydedilemedi. Lütfen tekrar deneyin.')
    } finally {
      setPending(false)
    }
  }

  const handleWithdraw = async () => {
    setWithdrawing(true)
    setWithdrawError(null)
    try {
      await withdrawBiometricConsent()
      setWithdrawOpen(false)
      setPhoto(null)
      showToast('Biyometrik rıza geri alındı. Yüz şablonu silindi.', 'success')
    } catch (caught) {
      setWithdrawError(caught instanceof ApiError ? caught.message : 'Rıza geri alınamadı. Lütfen tekrar deneyin.')
    } finally {
      setWithdrawing(false)
    }
  }

  return (
    <section className="card">
      <h1>Kayıt fotoğrafı</h1>
      <p className="auth-lead">
        Sınavdan önce kimliğiniz bu fotoğraftan üretilen yüz şablonuyla doğrulanır. Ham fotoğraf şablon çıkarıldıktan sonra saklanmaz.
      </p>

      <form className="narrow-form" onSubmit={handleSubmit} noValidate>
        <FormAlert message={error} />
        <label className="check">
          <input
            type="checkbox"
            checked={consent}
            onChange={(event) => {
              const checked = event.target.checked
              setConsent(checked)
              if (!checked) camera.stop()
              setError(null)
            }}
          />
          <span>Yüz fotoğrafımın kimlik doğrulaması için biyometrik şablona dönüştürülmesini kabul ediyorum.</span>
        </label>

        <div className="field">
          <label htmlFor="reference-photo">Fotoğraf</label>
          <input
            id="reference-photo"
            type="file"
            accept={ACCEPTED_TYPES.join(',')}
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file) remember(file)
            }}
          />
        </div>

        <div className="action-row">
          <Button type="button" variant="secondary" disabled={!consent} onClick={openCamera}>
            Kameradan çek
          </Button>
          {camera.live ? (
            <Button type="button" variant="secondary" onClick={takePhoto}>
              Bu kareyi kullan
            </Button>
          ) : null}
        </div>
        {camera.error ? <FormAlert message={camera.error} /> : null}
        <video ref={videoRef} className={camera.live ? 'camera-preview' : 'visually-hidden'} muted playsInline />
        {previewUrl ? <img className="camera-preview" src={previewUrl} alt="Seçilen kayıt fotoğrafı" /> : null}

        <Button type="submit" disabled={pending}>
          {pending ? 'Kaydediliyor…' : 'Fotoğrafı kaydet'}
        </Button>
      </form>

      <h2>Biyometrik rıza</h2>
      <p className="auth-lead">Rızayı geri alırsanız kayıtlı yüz şablonu silinir. Sınav sırasında geri alırsanız oturumunuz iptal edilir.</p>
      <Button variant="danger" onClick={() => setWithdrawOpen(true)}>
        Biyometrik rızayı geri al
      </Button>

      <Modal open={withdrawOpen} title="Rızayı geri al" onClose={() => setWithdrawOpen(false)}>
        <FormAlert message={withdrawError} />
        <p>Biyometrik rızanız geri alınır ve yüz şablonunuz silinir. Bu işlem geri alınamaz.</p>
        <div className="action-row">
          <Button variant="danger" disabled={withdrawing} onClick={() => void handleWithdraw()}>
            {withdrawing ? 'Geri alınıyor…' : 'Rızayı geri al'}
          </Button>
          <Button variant="secondary" onClick={() => setWithdrawOpen(false)}>
            Vazgeç
          </Button>
        </div>
      </Modal>
    </section>
  )
}
