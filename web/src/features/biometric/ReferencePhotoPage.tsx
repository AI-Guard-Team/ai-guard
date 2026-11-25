import { CircleCheck, CircleX, FileCheck2, ScanFace } from 'lucide-react'
import { motion } from 'motion/react'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { getBiometricStatus, uploadReferencePhoto, withdrawBiometricConsent } from '../../api/profile'
import { Alert, FormAlert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Checkbox } from '../../components/Checkbox'
import { Modal } from '../../components/Modal'
import { ProfileHero } from '../../pages/profile/ProfileHero'
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
  const [consentOpen, setConsentOpen] = useState(false)
  const [consentChecked, setConsentChecked] = useState(false)
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
      setConsentChecked(false)
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
      <ProfileHero />
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
              {!consent ? (
                // 1. adım: açık rıza (KVKK). Metin ve onay açılır pencerede; onaydan sonra fotoğraf alanı açılır.
                <div className="consent-gate">
                  <span className="consent-gate-icon" aria-hidden="true">
                    <FileCheck2 size={26} />
                  </span>
                  <div>
                    <strong>Önce açık rızanız gerekiyor</strong>
                    <p>Fotoğrafınızdan yüz şablonu çıkarılmadan önce biyometrik veri aydınlatma metnini okuyup onaylamanız gerekir.</p>
                  </div>
                  <Button onClick={() => setConsentOpen(true)} icon={<FileCheck2 size={18} aria-hidden="true" />}>
                    Metni oku ve onayla
                  </Button>
                </div>
              ) : (
                // 2. adım: fotoğraf (sürükle-bırak, dosya ya da kamera).
                <div className="photo-step">
                  <div className="photo-step-head">
                    <strong>Fotoğrafınızı buraya ekleyin</strong>
                    <button type="button" className="consent-given" onClick={() => setConsentOpen(true)}>
                      <CircleCheck size={16} aria-hidden="true" />
                      Açık rıza verildi · metni gör
                    </button>
                  </div>
                  <div className="photo-picker-slot">
                    <PhotoPicker disabled={false} previewUrl={previewUrl} onPick={pick} />
                  </div>
                </div>
              )}

              {error ? (
                <Alert tone="danger" title={error.title}>
                  {error.text}
                </Alert>
              ) : null}

              {consent ? (
                <div className="photo-submit">
                  <Button type="submit" size="lg" loading={pending || preparing} disabled={!photo}>
                    {pending ? 'Gönderiliyor…' : preparing ? 'Hazırlanıyor…' : enrolled ? 'Fotoğrafı güncelle' : 'Fotoğrafı kaydet'}
                  </Button>
                  {pending ? <span className="photo-submit-hint">Yüz şablonu çıkarılıyor; bu birkaç saniye sürebilir.</span> : null}
                </div>
              ) : null}
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
          <motion.span
            className="photo-guide-icon"
            aria-hidden="true"
            animate={{ y: [0, -6, 0] }}
            transition={{ duration: 3, repeat: Infinity, ease: 'easeInOut' }}
          >
            <ScanFace size={30} />
          </motion.span>
          <ul className="photo-rules">
            {PHOTO_RULES.map((rule, index) => (
              <motion.li
                key={rule.ok}
                initial={{ opacity: 0, x: 12 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ duration: 0.35, delay: 0.15 + index * 0.12, ease: [0.22, 1, 0.36, 1] }}
              >
                <span className="photo-rule-ok">
                  <CircleCheck size={16} aria-hidden="true" />
                  {rule.ok}
                </span>
                <span className="photo-rule-no">
                  <CircleX size={16} aria-hidden="true" />
                  {rule.no}
                </span>
              </motion.li>
            ))}
          </ul>
          <p className="field-hint">Fotoğraf uygun değilse sistem nedenini söyler; fotoğrafı değiştirip tekrar deneyebilirsiniz.</p>
        </Panel>
      </div>

      <Modal open={consentOpen} title="Biyometrik veri aydınlatma metni" onClose={() => setConsentOpen(false)}>
        <div className="consent-modal">
          <ConsentTextPanel resource={consentText} />
          {consent ? (
            <p className="consent-modal-done">
              <CircleCheck size={16} aria-hidden="true" /> Bu metne açık rıza verdiniz.
            </p>
          ) : (
            <>
              <Checkbox
                checked={consentChecked}
                disabled={!consentText.data}
                onChange={(event) => setConsentChecked(event.target.checked)}
                label="Metni okudum; yüz fotoğrafımdan kimlik doğrulaması için biyometrik şablon çıkarılmasına açık rıza veriyorum."
              />
              <div className="modal-actions">
                <Button variant="secondary" onClick={() => setConsentOpen(false)}>
                  Vazgeç
                </Button>
                <Button
                  disabled={!consentChecked}
                  onClick={() => {
                    setConsent(true)
                    setConsentOpen(false)
                    setError(null)
                  }}
                >
                  Onaylıyorum
                </Button>
              </div>
            </>
          )}
        </div>
      </Modal>

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
