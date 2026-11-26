import { Save, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError } from '../../api/client'
import { createExam, deleteExam, updateExam, type ExamRequest } from '../../api/admin'
import { EXAM_TYPE_LABEL, type ExamSummary, type ExamType } from '../../api/exams'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Input } from '../../components/Input'
import { InfoTip } from '../../components/InfoTip'
import { Modal } from '../../components/Modal'
import { useToast } from '../../components/useToast'
import { formatDateTime } from '../../lib/format'
import { common } from '../../i18n/common'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { WindowStrip } from './WindowStrip'

const MAX_DURATION = 600
const MAX_RETENTION = 3650

const messages = defineMessages({
  tr: {
    durationError: `Süre 1–${MAX_DURATION} dakika arasında olmalı.`,
    retentionError: `Saklama süresi 1–${MAX_RETENTION} gün arasında olmalı.`,
    windowOrder: 'Pencere bitişi başlangıçtan sonra olmalı.',
    windowShort: 'Pencere, sınav süresinden kısa olamaz.',
    nameRequired: 'Sınav adı zorunludur.',
    saved: 'Sınav bilgileri kaydedildi.',
    created: 'Sınav oluşturuldu. Şimdi soruları ekleyin.',
    saveFailed: 'Sınav kaydedilemedi.',
    deleted: 'Sınav silindi.',
    deleteFailed: 'Sınav silinemedi.',
    title: 'Genel bilgiler',
    name: 'Sınav adı',
    namePlaceholder: 'Örn. 2026 Güz Genel Yetenek Sınavı',
    type: 'Sınav türü',
    duration: 'Süre (dakika)',
    window: 'Erişim penceresi',
    windowInfo: 'Adaylar bu aralıkta sınava girebilir. Son başlangıç saati, pencere bitişinden sınav süresi çıkarılarak bulunur; bu saatten sonra yeni aday başlayamaz.',
    start: 'Başlangıç',
    end: 'Bitiş',
    latestStart: 'Son başlangıç:',
    retention: 'Kanıt saklama süresi (gün)',
    retentionHint: 'Süre dolunca kanıt kareleri, olaylar ve yüz vektörü otomatik silinir; sonuç ve karar kalır.',
    delete: 'Sınavı sil',
    saveChanges: 'Değişiklikleri kaydet',
    saveContinue: 'Kaydet ve devam et',
    confirmTitle: 'Sınav silinsin mi?',
    confirmText: (name: string) => `"${name}" ve soruları silinecek. Oturumu olan sınav silinemez; sunucu bu durumda nedenini söyler.`,
  },
  en: {
    durationError: `Duration must be between 1 and ${MAX_DURATION} minutes.`,
    retentionError: `Retention must be between 1 and ${MAX_RETENTION} days.`,
    windowOrder: 'The window must end after it starts.',
    windowShort: 'The window cannot be shorter than the exam duration.',
    nameRequired: 'Exam name is required.',
    saved: 'Exam details saved.',
    created: 'Exam created. Now add the questions.',
    saveFailed: 'The exam could not be saved.',
    deleted: 'Exam deleted.',
    deleteFailed: 'The exam could not be deleted.',
    title: 'General details',
    name: 'Exam name',
    namePlaceholder: 'e.g. Fall 2026 General Aptitude Exam',
    type: 'Exam type',
    duration: 'Duration (minutes)',
    window: 'Access window',
    windowInfo: 'Candidates can take the exam within this window. The latest start time is the window end minus the exam duration; after it, no new candidate can start.',
    start: 'Start',
    end: 'End',
    latestStart: 'Latest start:',
    retention: 'Evidence retention (days)',
    retentionHint: 'When the period ends, evidence frames, events and face vectors are deleted automatically; the result and decision remain.',
    delete: 'Delete exam',
    saveChanges: 'Save changes',
    saveContinue: 'Save and continue',
    confirmTitle: 'Delete this exam?',
    confirmText: (name: string) => `"${name}" and its questions will be deleted. An exam with sessions cannot be deleted; the server will say so.`,
  },
})

/** 1 ile `max` arasında tam sayı mı. */
const inRange = (text: string, max: number) => {
  const value = Number(text)
  return text.trim() !== '' && Number.isInteger(value) && value >= 1 && value <= max
}

const pad = (value: number) => String(value).padStart(2, '0')

/** ISO → `datetime-local` (yerel saat). */
function toLocalInput(value: string | Date): string {
  const d = typeof value === 'string' ? new Date(value) : value
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function defaults(): ExamRequest {
  const start = new Date()
  start.setDate(start.getDate() + 1)
  start.setHours(10, 0, 0, 0)
  const end = new Date(start)
  end.setHours(18)
  return { name: '', type: 'GeneralAptitude', durationMinutes: 60, windowStartAt: start.toISOString(), windowEndAt: end.toISOString(), retentionDays: 30 }
}

interface ExamGeneralFormProps {
  exam: ExamSummary | null
  onSaved: () => void
}

/**
 * Sınavın genel bilgileri (Stitch 26). Doğrulama sunucuda (Türkçe mesaj `detail`'de); burada yalnızca boş alanlar
 * ve pencere sırası önden söyleniyor. Son başlangıç = pencere bitişi − süre (FR-06), formda anlık gösteriliyor.
 */
export function ExamGeneralForm({ exam, onSaved }: ExamGeneralFormProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const navigate = useNavigate()
  const showToast = useToast()
  const initial = exam
    ? { name: exam.name, type: exam.type, durationMinutes: exam.durationMinutes, windowStartAt: exam.windowStartAt, windowEndAt: exam.windowEndAt, retentionDays: exam.retentionDays }
    : defaults()
  const [form, setForm] = useState<ExamRequest>(initial)
  // Alanların ham metni: yazarken (yıl yarımken, sayı silinince) değer bozulmasın; geçerli olunca forma yansır.
  const [startText, setStartText] = useState(() => toLocalInput(initial.windowStartAt))
  const [endText, setEndText] = useState(() => toLocalInput(initial.windowEndAt))
  const [durationText, setDurationText] = useState(String(initial.durationMinutes))
  const [retentionText, setRetentionText] = useState(String(initial.retentionDays))
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const set = <K extends keyof ExamRequest>(key: K, value: ExamRequest[K]) => setForm((current) => ({ ...current, [key]: value }))
  const setDate = (key: 'windowStartAt' | 'windowEndAt', text: string) => {
    ;(key === 'windowStartAt' ? setStartText : setEndText)(text)
    const date = new Date(text)
    if (!text || Number.isNaN(date.getTime()) || date.getFullYear() < 2000) return
    set(key, date.toISOString())
    // Başlangıç bitişi geçerse bitiş, pencerenin uzunluğunu koruyarak birlikte kayar.
    if (key === 'windowStartAt') {
      const span = new Date(form.windowEndAt).getTime() - new Date(form.windowStartAt).getTime()
      if (date.getTime() >= new Date(form.windowEndAt).getTime() && span > 0) {
        const end = new Date(date.getTime() + span)
        set('windowEndAt', end.toISOString())
        setEndText(toLocalInput(end))
      }
    }
  }
  const setNumber = (key: 'durationMinutes' | 'retentionDays', text: string) => {
    ;(key === 'durationMinutes' ? setDurationText : setRetentionText)(text)
    if (inRange(text, key === 'durationMinutes' ? MAX_DURATION : MAX_RETENTION)) set(key, Number(text))
  }
  const durationError = inRange(durationText, MAX_DURATION) ? undefined : m.durationError
  const retentionError = inRange(retentionText, MAX_RETENTION) ? undefined : m.retentionError

  const start = new Date(form.windowStartAt)
  const end = new Date(form.windowEndAt)
  const latestStart = new Date(end.getTime() - form.durationMinutes * 60_000)
  const windowError = end <= start ? m.windowOrder : latestStart < start ? m.windowShort : undefined

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (pending) return
    const problem = !form.name.trim() ? m.nameRequired : (durationError ?? retentionError ?? windowError)
    if (problem) {
      setError(problem)
      return
    }
    setPending(true)
    setError(null)
    try {
      const body = { ...form, name: form.name.trim() }
      if (exam) {
        await updateExam(exam.id, body)
        showToast(m.saved, 'success')
        onSaved()
      } else {
        const created = await createExam(body)
        showToast(m.created, 'success')
        navigate(`/admin/exams/${created.id}/questions`)
      }
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : m.saveFailed)
    } finally {
      setPending(false)
    }
  }

  const remove = async () => {
    if (!exam) return
    setPending(true)
    try {
      await deleteExam(exam.id)
      showToast(m.deleted, 'success')
      navigate('/admin/exams', { replace: true })
    } catch (caught) {
      setConfirmDelete(false)
      setError(caught instanceof ApiError ? caught.message : m.deleteFailed)
      setPending(false)
    }
  }

  return (
    <form className="editor-card" onSubmit={submit} noValidate>
      <h2 className="editor-card-title">{m.title}</h2>

      <Input
        label={m.name}
        value={form.name}
        maxLength={200}
        placeholder={m.namePlaceholder}
        error={error === m.nameRequired ? m.nameRequired : undefined}
        onChange={(event) => set('name', event.target.value)}
      />

      <div className="form-grid">
        <label className="field">
          <span className="field-label">{m.type}</span>
          <select className="select" value={form.type} onChange={(event) => set('type', event.target.value as ExamType)}>
            {(Object.keys(EXAM_TYPE_LABEL) as ExamType[]).map((type) => (
              <option key={type} value={type}>
                {EXAM_TYPE_LABEL[type]}
              </option>
            ))}
          </select>
        </label>
        <Input
          label={m.duration}
          type="number"
          min={1}
          max={MAX_DURATION}
          inputMode="numeric"
          value={durationText}
          error={durationError}
          onChange={(event) => setNumber('durationMinutes', event.target.value)}
        />
      </div>

      <fieldset className="form-fieldset">
        <legend>
          {m.window}{' '}
          <InfoTip title={m.window}>
            <p>{m.windowInfo}</p>
          </InfoTip>
        </legend>
        <div className="form-grid">
          <Input label={m.start} type="datetime-local" lang={getLocale()} value={startText} onChange={(event) => setDate('windowStartAt', event.target.value)} />
          <Input
            label={m.end}
            type="datetime-local"
            lang={getLocale()}
            value={endText}
            error={windowError}
            onChange={(event) => setDate('windowEndAt', event.target.value)}
          />
        </div>
        {!windowError ? (
          <>
            <WindowStrip start={start} end={end} latestStart={latestStart} />
            <p className="form-note">
              {m.latestStart} <strong>{formatDateTime(latestStart)}</strong>
            </p>
          </>
        ) : null}
      </fieldset>

      <Input
        label={m.retention}
        type="number"
        min={1}
        max={MAX_RETENTION}
        inputMode="numeric"
        hint={m.retentionHint}
        value={retentionText}
        error={retentionError}
        onChange={(event) => setNumber('retentionDays', event.target.value)}
      />

      {error ? <Alert tone="danger">{error}</Alert> : null}

      <div className="editor-actions">
        {exam ? (
          <Button variant="ghost" icon={<Trash2 size={18} aria-hidden="true" />} onClick={() => setConfirmDelete(true)} disabled={pending}>
            {m.delete}
          </Button>
        ) : (
          <span />
        )}
        <Button type="submit" loading={pending} icon={<Save size={18} aria-hidden="true" />}>
          {exam ? m.saveChanges : m.saveContinue}
        </Button>
      </div>

      <Modal open={confirmDelete} title={m.confirmTitle} onClose={() => setConfirmDelete(false)}>
        <p>{m.confirmText(exam?.name ?? '')}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmDelete(false)}>
            {c.cancel}
          </Button>
          <Button variant="danger" loading={pending} onClick={() => void remove()}>
            {m.delete}
          </Button>
        </div>
      </Modal>
    </form>
  )
}
