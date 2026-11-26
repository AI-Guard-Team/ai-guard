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
import { WindowStrip } from './WindowStrip'

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
    const value = Number(text)
    if (text.trim() && Number.isInteger(value) && value > 0) set(key, value)
  }
  const durationError = Number(durationText) > 0 ? undefined : 'Süre en az 1 dakika olmalı.'
  const retentionError = Number(retentionText) > 0 ? undefined : 'Saklama süresi en az 1 gün olmalı.'

  const start = new Date(form.windowStartAt)
  const end = new Date(form.windowEndAt)
  const latestStart = new Date(end.getTime() - form.durationMinutes * 60_000)
  const windowError = end <= start ? 'Pencere bitişi başlangıçtan sonra olmalı.' : latestStart < start ? 'Pencere, sınav süresinden kısa olamaz.' : undefined

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const problem = !form.name.trim() ? 'Sınav adı zorunludur.' : (durationError ?? retentionError ?? windowError)
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
        showToast('Sınav bilgileri kaydedildi.', 'success')
        onSaved()
      } else {
        const created = await createExam(body)
        showToast('Sınav oluşturuldu. Şimdi soruları ekleyin.', 'success')
        navigate(`/admin/exams/${created.id}/questions`)
      }
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Sınav kaydedilemedi.')
    } finally {
      setPending(false)
    }
  }

  const remove = async () => {
    if (!exam) return
    setPending(true)
    try {
      await deleteExam(exam.id)
      showToast('Sınav silindi.', 'success')
      navigate('/admin/exams', { replace: true })
    } catch (caught) {
      setConfirmDelete(false)
      setError(caught instanceof ApiError ? caught.message : 'Sınav silinemedi.')
      setPending(false)
    }
  }

  return (
    <form className="editor-card" onSubmit={submit} noValidate>
      <h2 className="editor-card-title">Genel bilgiler</h2>

      <Input label="Sınav adı" value={form.name} maxLength={200} placeholder="Örn. 2026 Güz Genel Yetenek Sınavı" onChange={(event) => set('name', event.target.value)} />

      <div className="form-grid">
        <label className="field">
          <span className="field-label">Sınav türü</span>
          <select className="select" value={form.type} onChange={(event) => set('type', event.target.value as ExamType)}>
            {(Object.keys(EXAM_TYPE_LABEL) as ExamType[]).map((type) => (
              <option key={type} value={type}>
                {EXAM_TYPE_LABEL[type]}
              </option>
            ))}
          </select>
        </label>
        <Input
          label="Süre (dakika)"
          type="number"
          min={1}
          max={600}
          inputMode="numeric"
          value={durationText}
          error={durationError}
          onChange={(event) => setNumber('durationMinutes', event.target.value)}
        />
      </div>

      <fieldset className="form-fieldset">
        <legend>
          Erişim penceresi{' '}
          <InfoTip title="Erişim penceresi">
            <p>Adaylar bu aralıkta sınava girebilir. Son başlangıç saati, pencere bitişinden sınav süresi çıkarılarak bulunur; bu saatten sonra yeni aday başlayamaz.</p>
          </InfoTip>
        </legend>
        <div className="form-grid">
          <Input label="Başlangıç" type="datetime-local" value={startText} onChange={(event) => setDate('windowStartAt', event.target.value)} />
          <Input
            label="Bitiş"
            type="datetime-local"
            value={endText}
            error={windowError}
            onChange={(event) => setDate('windowEndAt', event.target.value)}
          />
        </div>
        {!windowError ? (
          <>
            <WindowStrip start={start} end={end} latestStart={latestStart} />
            <p className="form-note">
              Son başlangıç: <strong>{formatDateTime(latestStart)}</strong>
            </p>
          </>
        ) : null}
      </fieldset>

      <Input
        label="Kanıt saklama süresi (gün)"
        type="number"
        min={1}
        max={3650}
        inputMode="numeric"
        hint="Süre dolunca kanıt kareleri, olaylar ve yüz vektörü otomatik silinir; sonuç ve karar kalır."
        value={retentionText}
        error={retentionError}
        onChange={(event) => setNumber('retentionDays', event.target.value)}
      />

      {error ? <Alert tone="danger">{error}</Alert> : null}

      <div className="editor-actions">
        {exam ? (
          <Button variant="ghost" icon={<Trash2 size={18} aria-hidden="true" />} onClick={() => setConfirmDelete(true)} disabled={pending}>
            Sınavı sil
          </Button>
        ) : (
          <span />
        )}
        <Button type="submit" loading={pending} icon={<Save size={18} aria-hidden="true" />}>
          {exam ? 'Değişiklikleri kaydet' : 'Kaydet ve devam et'}
        </Button>
      </div>

      <Modal open={confirmDelete} title="Sınav silinsin mi?" onClose={() => setConfirmDelete(false)}>
        <p>"{exam?.name}" ve soruları silinecek. Oturumu olan sınav silinemez; sunucu bu durumda nedenini söyler.</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmDelete(false)}>
            Vazgeç
          </Button>
          <Button variant="danger" loading={pending} onClick={() => void remove()}>
            Sınavı sil
          </Button>
        </div>
      </Modal>
    </form>
  )
}
