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
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import { PhotoPicker } from './PhotoPicker'
import { PhotoPrepareError, prepareReferencePhoto, rejectedPhotoType } from './prepareReferencePhoto'
import './ReferencePhotoPage.css'

const messages = defineMessages({
  tr: {
    // Görüntü servisinin reddettiği durumlar (K-29): birden fazla yüz, yüz yok, bulanık / yandan.
    rules: [
      { ok: 'Karede yalnızca siz', no: 'Yanınızda başka biri ya da fotoğraf' },
      { ok: 'Kameraya düz bakış, net görüntü', no: 'Yandan, eğik ya da bulanık' },
      { ok: 'Yüze önden gelen dengeli ışık', no: 'Arkadan ışık, çok karanlık' },
      { ok: 'Yüz tamamen açık', no: 'Koyu gözlük, şapka, maske' },
    ],
    statusFailed: 'Kayıt fotoğrafı durumu yüklenemedi.',
    processFailed: 'Fotoğraf işlenemedi. Lütfen başka bir dosya deneyin.',
    consentRequired: 'Biyometrik veri için açık rıza vermeden fotoğraf kaydedilemez.',
    photoRequired: 'Bir fotoğraf seçin ya da kameradan çekin.',
    saved: 'Kayıt fotoğrafınız işlendi. Yüz şablonunuz hazır.',
    changePhoto: 'Fotoğrafı değiştirip tekrar deneyin.',
    saveFailed: 'Fotoğraf kaydedilemedi. Lütfen tekrar deneyin.',
    withdrawn: 'Biyometrik rızanız geri alındı ve yüz şablonunuz silindi.',
    withdrawFailed: 'Rıza geri alınamadı. Lütfen tekrar deneyin.',
    title: 'Kayıt fotoğrafı ve biyometrik rıza',
    description: 'Sınav başında kimliğiniz bu fotoğraftan çıkarılan yüz şablonuyla doğrulanır.',
    ready: 'Şablon hazır',
    none: 'Fotoğraf yok',
    readyTitle: 'Yüz şablonunuz hazır',
    readyText: (at: string) =>
      `${at} tarihinde oluşturuldu. Ham fotoğrafınız şablon çıkarıldıktan sonra saklanmadı; yalnızca şifreli şablon tutuluyor. Yeni fotoğraf yüklerseniz eski şablonun yerini alır.`,
    gateTitle: 'Önce açık rızanız gerekiyor',
    gateText: 'Fotoğrafınızdan yüz şablonu çıkarılmadan önce biyometrik veri aydınlatma metnini okuyup onaylamanız gerekir.',
    gateAction: 'Metni oku ve onayla',
    addPhoto: 'Fotoğrafınızı buraya ekleyin',
    consentGiven: 'Açık rıza verildi · metni gör',
    sending: 'Gönderiliyor…',
    preparing: 'Hazırlanıyor…',
    update: 'Fotoğrafı güncelle',
    save: 'Fotoğrafı kaydet',
    extracting: 'Yüz şablonu çıkarılıyor; bu birkaç saniye sürebilir.',
    withdrawTitle: 'Biyometrik rızayı geri al',
    withdrawText: 'Yüz şablonunuz hemen silinir. Sınav sırasında geri alırsanız oturumunuz iptal edilir.',
    withdraw: 'Rızamı geri al',
    guide: 'İyi bir fotoğraf için',
    guideHint: 'Fotoğraf uygun değilse sistem nedenini söyler; fotoğrafı değiştirip tekrar deneyebilirsiniz.',
    consentTitle: 'Biyometrik veri aydınlatma metni',
    consented: 'Bu metne açık rıza verdiniz.',
    consentLabel: 'Metni okudum; yüz fotoğrafımdan kimlik doğrulaması için biyometrik şablon çıkarılmasına açık rıza veriyorum.',
    approve: 'Onaylıyorum',
    withdrawConfirm: 'Yüz şablonunuz silinir. Yeni fotoğraf yükleyene kadar sınavlara başlayamazsınız. Bu işlem geri alınamaz.',
    withdrawing: 'Geri alınıyor…',
  },
  en: {
    rules: [
      { ok: 'Only you in the frame', no: 'Someone or a photo next to you' },
      { ok: 'Looking straight at the camera, sharp image', no: 'From the side, tilted or blurry' },
      { ok: 'Even light from the front', no: 'Backlit or too dark' },
      { ok: 'Face fully visible', no: 'Dark glasses, hat, mask' },
    ],
    statusFailed: 'The reference photo status could not be loaded.',
    processFailed: 'The photo could not be processed. Please try another file.',
    consentRequired: 'The photo cannot be saved without explicit consent for biometric data.',
    photoRequired: 'Choose a photo or take one with the camera.',
    saved: 'Your reference photo has been processed. Your face template is ready.',
    changePhoto: 'Change the photo and try again.',
    saveFailed: 'The photo could not be saved. Please try again.',
    withdrawn: 'Your biometric consent was withdrawn and your face template deleted.',
    withdrawFailed: 'Consent could not be withdrawn. Please try again.',
    title: 'Reference photo and biometric consent',
    description: 'At the start of each exam, your identity is verified with the face template extracted from this photo.',
    ready: 'Template ready',
    none: 'No photo',
    readyTitle: 'Your face template is ready',
    readyText: (at: string) =>
      `Created on ${at}. Your original photo was not kept after the template was extracted; only the encrypted template is stored. Uploading a new photo replaces the old template.`,
    gateTitle: 'Your explicit consent comes first',
    gateText: 'Before a face template is extracted from your photo, you need to read and accept the biometric data privacy notice.',
    gateAction: 'Read and accept',
    addPhoto: 'Add your photo here',
    consentGiven: 'Consent given · view text',
    sending: 'Uploading…',
    preparing: 'Preparing…',
    update: 'Update photo',
    save: 'Save photo',
    extracting: 'Extracting the face template; this can take a few seconds.',
    withdrawTitle: 'Withdraw biometric consent',
    withdrawText: 'Your face template is deleted immediately. If you withdraw during an exam, your session is cancelled.',
    withdraw: 'Withdraw my consent',
    guide: 'For a good photo',
    guideHint: 'If the photo is not suitable, the system tells you why; you can change it and try again.',
    consentTitle: 'Biometric data privacy notice',
    consented: 'You have given explicit consent to this text.',
    consentLabel: 'I have read the text and give explicit consent to extracting a biometric template from my face photo for identity verification.',
    approve: 'I agree',
    withdrawConfirm: 'Your face template will be deleted. You cannot start exams until you upload a new photo. This cannot be undone.',
    withdrawing: 'Withdrawing…',
  },
})

/** Sunucunun reddi rıza metninin eskimesinden mi (A3: "Rıza metni güncel değil"); dilden bağımsız, ham gövdeden okunur. */
function staleConsentText(error: ApiError): boolean {
  const title = (error.body as { title?: unknown } | undefined)?.title
  return error.status === 400 && typeof title === 'string' && title.includes('Rıza metni')
}

/** Kayıt fotoğrafı ve biyometrik rıza (Stitch ekran 8; A3 uçları, K-27, K-36). */
export function ReferencePhotoPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const showToast = useToast()
  const consentText = useConsentText('Biometric')
  const loadStatus = useCallback((signal: AbortSignal) => getBiometricStatus(signal), [])
  const status = useResource(loadStatus, m.statusFailed)
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
        setError({ text: caught instanceof PhotoPrepareError ? caught.message : m.processFailed })
      },
    )
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    const version = consentText.data?.version
    if (!consent || !version) {
      setError({ text: m.consentRequired })
      return
    }
    if (!photo) {
      setError({ text: m.photoRequired })
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
      showToast(m.saved, 'success')
    } catch (caught) {
      // Sunucu nedeni "title"da verir (yüz bulunamadı, rıza metni eski, sınav sürüyor); ne yapılacağı "detail"de.
      setError(
        caught instanceof ApiError
          ? { title: caught.title, text: caught.message === caught.title ? m.changePhoto : caught.message }
          : { text: m.saveFailed },
      )
      if (caught instanceof ApiError && staleConsentText(caught)) consentText.reload()
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
      showToast(m.withdrawn, 'success')
    } catch (caught) {
      setWithdrawError(caught instanceof ApiError ? caught.message : m.withdrawFailed)
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
            title={m.title}
            description={m.description}
            aside={
              status.loading && !status.data ? (
                <Skeleton width="7rem" height="1.5rem" radius="lg" />
              ) : (
                <StatusBadge tone={enrolled ? 'success' : 'warning'} dot>
                  {enrolled ? m.ready : m.none}
                </StatusBadge>
              )
            }
          >
            {status.error ? <Alert tone="danger">{status.error}</Alert> : null}
            {enrolled && status.data?.enrolledAt ? (
              <Alert tone="success" live={false} title={m.readyTitle}>
                <p>{m.readyText(formatDateTime(status.data.enrolledAt))}</p>
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
                    <strong>{m.gateTitle}</strong>
                    <p>{m.gateText}</p>
                  </div>
                  <Button onClick={() => setConsentOpen(true)} icon={<FileCheck2 size={18} aria-hidden="true" />}>
                    {m.gateAction}
                  </Button>
                </div>
              ) : (
                // 2. adım: fotoğraf (sürükle-bırak, dosya ya da kamera).
                <div className="photo-step">
                  <div className="photo-step-head">
                    <strong>{m.addPhoto}</strong>
                    <button type="button" className="consent-given" onClick={() => setConsentOpen(true)}>
                      <CircleCheck size={16} aria-hidden="true" />
                      {m.consentGiven}
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
                    {pending ? m.sending : preparing ? m.preparing : enrolled ? m.update : m.save}
                  </Button>
                  {pending ? <span className="photo-submit-hint">{m.extracting}</span> : null}
                </div>
              ) : null}
            </form>
          </Panel>

          {enrolled ? (
            <Panel title={m.withdrawTitle} description={m.withdrawText}>
              <Button variant="danger" onClick={() => setWithdrawOpen(true)}>
                {m.withdraw}
              </Button>
            </Panel>
          ) : null}
        </div>

        <Panel title={m.guide} tone="accent" className="photo-guide" headingLevel={2}>
          <motion.span
            className="photo-guide-icon"
            aria-hidden="true"
            animate={{ y: [0, -6, 0] }}
            transition={{ duration: 3, repeat: Infinity, ease: 'easeInOut' }}
          >
            <ScanFace size={30} />
          </motion.span>
          <ul className="photo-rules">
            {m.rules.map((rule, index) => (
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
          <p className="field-hint">{m.guideHint}</p>
        </Panel>
      </div>

      <Modal open={consentOpen} title={m.consentTitle} onClose={() => setConsentOpen(false)}>
        <div className="consent-modal">
          <ConsentTextPanel resource={consentText} />
          {consent ? (
            <p className="consent-modal-done">
              <CircleCheck size={16} aria-hidden="true" /> {m.consented}
            </p>
          ) : (
            <>
              <Checkbox
                checked={consentChecked}
                disabled={!consentText.data}
                onChange={(event) => setConsentChecked(event.target.checked)}
                label={m.consentLabel}
              />
              <div className="modal-actions">
                <Button variant="secondary" onClick={() => setConsentOpen(false)}>
                  {c.cancel}
                </Button>
                <Button
                  disabled={!consentChecked}
                  onClick={() => {
                    setConsent(true)
                    setConsentOpen(false)
                    setError(null)
                  }}
                >
                  {m.approve}
                </Button>
              </div>
            </>
          )}
        </div>
      </Modal>

      <Modal open={withdrawOpen} title={m.withdrawTitle} onClose={() => setWithdrawOpen(false)}>
        <FormAlert message={withdrawError} />
        <p>{m.withdrawConfirm}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setWithdrawOpen(false)}>
            {c.cancel}
          </Button>
          <Button variant="danger" loading={withdrawing} onClick={() => void withdraw()}>
            {withdrawing ? m.withdrawing : m.withdraw}
          </Button>
        </div>
      </Modal>
    </>
  )
}
