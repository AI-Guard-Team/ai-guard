import { CircleCheck, CircleX } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { getBiometricStatus, uploadReferencePhoto, withdrawBiometricConsent } from '../../api/profile'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Checkbox } from '../../components/Checkbox'
import { Modal } from '../../components/Modal'
import { PageHeader } from '../../components/PageHeader'
import { Panel } from '../../components/Panel'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { ProfileTabs } from '../../pages/profile/ProfileTabs'
import { ConsentTextPanel } from '../consent/ConsentTextPanel'
import { useConsentText } from '../consent/useConsentText'
import { formatDateTime } from '../exams/examStatus'
import { PhotoPicker } from './PhotoPicker'
import { PhotoPrepareError, prepareReferencePhoto, rejectedPhotoType } from './prepareReferencePhoto'
import './ReferencePhotoPage.css'

// Görüntü servisinin reddettiği durumlar (K-29): birden fazla yüz, yüz yok, bulanık / yandan.
const PHOTO_RULES = [
  { ok: 'Karede yalnızca siz', no: 'Yanınızda başka biri ya da fotoğraf' },
  { ok: 'Kameraya düz bakış, net görüntü', no: 'Yandan, eğik ya da bulanık' },
  { ok: 'Yüze önden gelen dengeli ışık', no: 'Arkadan ışık, çok karanlık' },
  { ok: 'Yüz tamamen açık', no: 'Koyu gözlük, şapka, maske' },
] as const

/** Kayıt fotoğrafı ve biyometrik rıza (Stitch ekran 8; A3 uçları, K-27, K-35). */
export function ReferencePhotoPage() {
  const showToast = useToast()
  const consentText = useConsentText('Biometric')
  const loadStatus = useCallback((signal: AbortSignal) => getBiometricStatus(signal), [])
  const status = useResource(loadStatus, 'Kayıt fotoğrafı durumu yüklenemedi.')
  const [consent, setConsent] = useState(false)
  const [photo, setPhoto] = useState<File | null>(null)
  const [error, setError] = useState<{ title?: string; text: string } | null>(null)
  const [preparing, setPreparing] = useState(false)
  const [pending, setPending] = useState(false)
  const [withdrawOpen, setWithdrawOpen] = useState(false)
  const [withdrawError, setWithdrawError] = useState<string | null>(null)
  const [withdrawing, setWithdrawing] = useState(false)
  const generation = useRef(0)

  const previewUrl = useMemo(() => (photo ? URL.createObjectURL(photo) : null), [photo])
  useEffect(() => {
    if (!previewUrl) return
    return () => URL.revokeObjectURL(previewUrl)
  }, [previewUrl])

  const enrolled = status.data?.enrolled === true

  const pick = (file: File) => {
    const typeError = rejectedPhotoType(file)
    generation.current += 1
    setPhoto(null)
    if (typeError) {
      setPreparing(false)
      setError({ text: typeError })
      return
    }
    const id = generation.current
    setError(null)
    setPreparing(true)
    // Büyük fotoğraf göndermeden önce tarayıcıda küçültülür (S2); geç biten eski hazırlık yenisinin üstüne yazmaz.
    void prepareReferencePhoto(file).then(
      (prepared) => {
        if (id !== generation.current) return
        setPhoto(prepared)
        setPreparing(false)
      },
      (caught) => {
        if (id !== generation.current) return
        setPreparing(false)
        setError({ text: caught instanceof PhotoPrepareError ? caught.message : 'Fotoğraf işlenemedi. Lütfen başka bir dosya deneyin.' })
      },
    )
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    const version = consentText.data?.version
    if (!consent || !version) {
      setError({ text: 'Biyometrik veri için açık rıza vermeden fotoğraf kaydedilemez.' })
      return
    }
    if (!photo) {
      setError({ text: 'Bir fotoğraf seçin ya da kameradan çekin.' })
      return
    }
    setPending(true)
    setError(null)
    try {
      await uploadReferencePhoto(photo, version)
      setPhoto(null)
      setConsent(false)
      status.reload()
      showToast('Kayıt fotoğrafınız işlendi. Yüz şablonunuz hazır.', 'success')
    } catch (caught) {
      // Sunucu nedeni "title"da verir (yüz bulunamadı, rıza metni eski, sınav sürüyor); ne yapılacağı "detail"de.
      setError(
        caught instanceof ApiError
          ? { title: caught.title, text: caught.message === caught.title ? 'Fotoğrafı değiştirip tekrar deneyin.' : caught.message }
          : { text: 'Fotoğraf kaydedilemedi. Lütfen tekrar deneyin.' },
      )
      if (caught instanceof ApiError && caught.status === 400 && caught.title?.includes('Rıza metni')) consentText.reload()
    } finally {
      setPending(false)
    }
  }

  const withdraw = async () => {
    setWithdrawing(true)
    setWithdrawError(null)
    try {
      await withdrawBiometricConsent()
      setWithdrawOpen(false)
      status.reload()
      showToast('Biyometrik rızanız geri alındı ve yüz şablonunuz silindi.', 'success')
    } catch (caught) {
      setWithdrawError(caught instanceof ApiError ? caught.message : 'Rıza geri alınamadı. Lütfen tekrar deneyin.')
    } finally {
      setWithdrawing(false)
    }
  }

  return (
    <>
      <PageHeader title="Profil" description="Hesap bilgileriniz ve kayıt fotoğrafınız." />
      <ProfileTabs />

      <div className="photo-layout">
        <div className="photo-main">
          <Panel
            title="Kayıt fotoğrafı ve biyometrik rıza"
            description="Sınav başında kimliğiniz bu fotoğraftan çıkarılan yüz şablonuyla doğrulanır."
            aside={
              status.loading && !status.data ? (
                <Skeleton width="7rem" height="1.5rem" radius="lg" />
              ) : (
                <StatusBadge tone={enrolled ? 'success' : 'warning'} dot>
                  {enrolled ? 'Şablon hazır' : 'Fotoğraf yok'}
                </StatusBadge>
              )
            }
          >
            {status.error ? <Alert tone="danger">{status.error}</Alert> : null}
            {enrolled && status.data?.enrolledAt ? (
              <Alert tone="success" live={false} title="Yüz şablonunuz hazır">
                <p>
                  {formatDateTime(status.data.enrolledAt)} tarihinde oluşturuldu. Ham fotoğrafınız şablon çıkarıldıktan sonra saklanmadı; yalnızca şifreli şablon
                  tutuluyor. Yeni fotoğraf yüklerseniz eski şablonun yerini alır.
                </p>
              </Alert>
            ) : null}

            <form onSubmit={save} noValidate>
              <ConsentTextPanel resource={consentText} />
              <Checkbox
                checked={consent}
                disabled={!consentText.data}
                onChange={(event) => {
                  setConsent(event.target.checked)
                  setError(null)
                }}
                label="Yukarıdaki metni okudum; yüz fotoğrafımdan kimlik doğrulaması için biyometrik şablon çıkarılmasına açık rıza veriyorum."
              />

              <div className="photo-picker-slot">
                <PhotoPicker disabled={!consent} previewUrl={previewUrl} onPick={pick} />
              </div>

              {error ? (
                <Alert tone="danger" title={error.title}>
                  {error.text}
                </Alert>
              ) : null}

              <Button type="submit" size="lg" loading={pending || preparing} disabled={!consent || !photo}>
                {pending ? 'Gönderiliyor…' : preparing ? 'Hazırlanıyor…' : enrolled ? 'Fotoğrafı güncelle' : 'Fotoğrafı kaydet'}
              </Button>
            </form>
          </Panel>

          {enrolled ? (
            <Panel title="Biyometrik rızayı geri al" description="Yüz şablonunuz hemen silinir. Sınav sırasında geri alırsanız oturumunuz iptal edilir.">
              <Button variant="danger" onClick={() => setWithdrawOpen(true)}>
                Rızamı geri al
              </Button>
            </Panel>
          ) : null}
        </div>

        <Panel title="İyi bir fotoğraf için" tone="accent" className="photo-guide" headingLevel={2}>
          <ul className="photo-rules">
            {PHOTO_RULES.map((rule) => (
              <li key={rule.ok}>
                <span className="photo-rule-ok">
                  <CircleCheck size={16} aria-hidden="true" />
                  {rule.ok}
                </span>
                <span className="photo-rule-no">
                  <CircleX size={16} aria-hidden="true" />
                  {rule.no}
                </span>
              </li>
            ))}
          </ul>
          <p className="field-hint">Fotoğraf uygun değilse sistem nedenini söyler; fotoğrafı değiştirip tekrar deneyebilirsiniz.</p>
        </Panel>
      </div>

      <Modal open={withdrawOpen} title="Biyometrik rızayı geri al" onClose={() => setWithdrawOpen(false)}>
        <FormAlert message={withdrawError} />
        <p>Yüz şablonunuz silinir. Yeni fotoğraf yükleyene kadar sınavlara başlayamazsınız. Bu işlem geri alınamaz.</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setWithdrawOpen(false)}>
            Vazgeç
          </Button>
          <Button variant="danger" loading={withdrawing} onClick={() => void withdraw()}>
            {withdrawing ? 'Geri alınıyor…' : 'Rızamı geri al'}
          </Button>
        </div>
      </Modal>
    </>
  )
}
