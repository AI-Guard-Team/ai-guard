import { Building2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { updatePlatformSettings, type PlatformSettings } from '../../api/management'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'
import { useToast } from '../../components/useToast'
import { invalidatePlatformSettings } from '../../hooks/usePlatformSettings'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import { LIMITS, toDraft, toRequest, validate, type PlatformDraft, type PlatformErrorCode, type PlatformField } from './platformSettingsForm'

const messages = defineMessages({
  tr: {
    title: 'Aday iletişimi ve ajan',
    sub: 'Adayların kurulum ve iptal ekranlarında gördüğü bilgiler.',
    alternative: 'Alternatif sınav yöntemi',
    alternativeHint: (n: number, max: number) => `İzleme koşullarını kabul etmeyen ya da oturumu iptal edilen aday bu metni görür. ${n}/${max}`,
    alternativePlaceholder: 'Örn. Gözetmenli salon sınavı için sınav koordinatörlüğüne e-posta ile başvurun.',
    email: 'Destek e-postası',
    phone: 'Destek telefonu',
    windows: 'Ajan indirme adresi (Windows)',
    mac: 'Ajan indirme adresi (macOS)',
    urlHint: 'Yalnızca https:// ile başlayan adres. Boş bırakılırsa adaya indirme bağlantısı gösterilmez.',
    updated: (at: string) => `Son güncelleme: ${at}`,
    neverUpdated: 'Henüz kaydedilmedi.',
    saved: 'Ayarlar kaydedildi.',
    unchanged: 'Değişiklik yok.',
    fix: 'İşaretli alanları düzeltin.',
    errors: {
      tooLong: `En fazla ${LIMITS.text} karakter.`,
      email: 'Geçerli bir e-posta adresi girin.',
      phone: 'Yalnızca rakam, boşluk ve + ( ) - kullanılabilir (3–40 karakter).',
      https: 'https:// ile başlayan geçerli bir adres girin.',
    } as Record<PlatformErrorCode, string>,
  },
  en: {
    title: 'Candidate contact and agent',
    sub: 'What candidates see on the setup and cancellation screens.',
    alternative: 'Alternative exam method',
    alternativeHint: (n: number, max: number) => `Shown to candidates who decline monitoring or whose session is cancelled. ${n}/${max}`,
    alternativePlaceholder: 'E.g. Email the exam office to book a supervised exam in the hall.',
    email: 'Support email',
    phone: 'Support phone',
    windows: 'Agent download address (Windows)',
    mac: 'Agent download address (macOS)',
    urlHint: 'Only addresses starting with https://. Leave empty to hide the download link from candidates.',
    updated: (at: string) => `Last updated: ${at}`,
    neverUpdated: 'Not saved yet.',
    saved: 'Settings saved.',
    unchanged: 'Nothing changed.',
    fix: 'Fix the highlighted fields.',
    errors: {
      tooLong: `At most ${LIMITS.text} characters.`,
      email: 'Enter a valid email address.',
      phone: 'Only digits, spaces and + ( ) - are allowed (3–40 characters).',
      https: 'Enter a valid address starting with https://.',
    },
  },
})

interface Props {
  settings: PlatformSettings
  onSaved: () => void
}

/** Platform ayarları (Stitch 31; `PUT /api/platform-settings`, K-49). Yalnızca yönetici. */
export function PlatformSettingsCard({ settings, onSaved }: Props) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const showToast = useToast()
  const [draft, setDraft] = useState<PlatformDraft>(() => toDraft(settings))
  const [touched, setTouched] = useState<Partial<Record<PlatformField, boolean>>>({})
  const [submitted, setSubmitted] = useState(false)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  // Kayıttan sonra sunucudaki hâl (kırpılmış değerler) forma geri yansısın.
  const [source, setSource] = useState(settings)
  if (source !== settings) {
    setSource(settings)
    setDraft(toDraft(settings))
  }

  const errors = validate(draft)
  const errorFor = (field: PlatformField) => ((submitted || touched[field]) && errors[field] ? m.errors[errors[field]] : undefined)
  const dirty = JSON.stringify(toRequest(draft)) !== JSON.stringify(toRequest(toDraft(settings)))

  const field = (name: PlatformField) => ({
    value: draft[name],
    onChange: (event: { target: { value: string } }) => setDraft({ ...draft, [name]: event.target.value }),
    onBlur: () => setTouched({ ...touched, [name]: true }),
    error: errorFor(name),
  })

  const save = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    setFormError(null)
    if (Object.keys(errors).length > 0) {
      setFormError(m.fix)
      return
    }
    if (!dirty) {
      showToast(m.unchanged, 'info')
      return
    }

    setSaving(true)
    try {
      await updatePlatformSettings(toRequest(draft))
      invalidatePlatformSettings()
      showToast(m.saved, 'success')
      setSubmitted(false)
      setTouched({})
      onSaved()
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : m.fix)
    } finally {
      setSaving(false)
    }
  }

  const alt = field('alternativeMethodText')

  return (
    <section className="editor-card settings-wide" aria-labelledby="settings-platform">
      <div className="settings-head">
        <span className="settings-icon" aria-hidden="true">
          <Building2 size={20} />
        </span>
        <div>
          <h2 className="editor-card-title" id="settings-platform">
            {m.title}
          </h2>
          <p className="editor-card-sub">
            {m.sub} {settings.updatedAt ? m.updated(formatDateTime(settings.updatedAt)) : m.neverUpdated}
          </p>
        </div>
      </div>

      <form className="platform-form" onSubmit={save} noValidate>
        <label className={['field', 'platform-text', alt.error && 'has-error'].filter(Boolean).join(' ')}>
          <span className="field-label">{m.alternative}</span>
          <textarea
            rows={4}
            maxLength={LIMITS.text + 200}
            placeholder={m.alternativePlaceholder}
            value={alt.value}
            onChange={alt.onChange}
            onBlur={alt.onBlur}
            aria-invalid={alt.error ? true : undefined}
            aria-describedby="platform-alt-message"
          />
          <span id="platform-alt-message" className={alt.error ? 'field-error' : 'field-hint'} role={alt.error ? 'alert' : undefined}>
            {alt.error ?? m.alternativeHint(draft.alternativeMethodText.trim().length, LIMITS.text)}
          </span>
        </label>

        <div className="platform-row">
          <Input label={m.email} type="email" autoComplete="off" maxLength={LIMITS.email} placeholder="sinav-destek@kurum.edu.tr" {...field('supportEmail')} />
          <Input label={m.phone} type="tel" autoComplete="off" maxLength={40} placeholder="+90 212 555 01 90" {...field('supportPhone')} />
        </div>
        <div className="platform-row">
          <Input label={m.windows} type="url" inputMode="url" maxLength={LIMITS.url} placeholder="https://kurum.edu.tr/aiguard-agent.exe" hint={m.urlHint} {...field('agentDownloadUrlWindows')} />
          <Input label={m.mac} type="url" inputMode="url" maxLength={LIMITS.url} placeholder="https://kurum.edu.tr/aiguard-agent.pkg" hint={m.urlHint} {...field('agentDownloadUrlMac')} />
        </div>

        {formError ? <Alert tone="danger">{formError}</Alert> : null}

        <div className="platform-actions">
          <Button type="submit" disabled={saving}>
            {saving ? c.saving : c.save}
          </Button>
          {dirty && !saving ? (
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setDraft(toDraft(settings))
                setTouched({})
                setSubmitted(false)
                setFormError(null)
              }}
            >
              {c.cancel}
            </Button>
          ) : null}
        </div>
      </form>
    </section>
  )
}
