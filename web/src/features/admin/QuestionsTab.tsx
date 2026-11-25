import { Eye, ListChecks, Pencil, Plus, Trash2, X } from 'lucide-react'
import { AnimatePresence, motion } from 'motion/react'
import { useCallback, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { addQuestion, deleteQuestion, listQuestions, updateQuestion, type QuestionDetail } from '../../api/admin'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { Modal } from '../../components/Modal'
import { OpticalBubble } from '../../components/OpticalBubble'
import { Skeleton } from '../../components/Skeleton'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'

const LETTERS = 'ABCDEFGHIJ'
const MIN_OPTIONS = 2
const MAX_OPTIONS = 10

interface Draft {
  id: string | null
  text: string
  options: string[]
  correctOptionIndex: number
}

const emptyDraft = (): Draft => ({ id: null, text: '', options: ['', '', '', ''], correctOptionIndex: 0 })

interface QuestionsTabProps {
  examId: string
  onChanged: () => void
}

/**
 * Soru bankası ve düzenleyici (Stitch 27). Liste doğru seçeneği gösterir (yalnızca yöneticiye döner, E3).
 * "Aday görünümü" soruyu sınav ekranındaki baloncuklu düzende, doğru seçenek işaretsiz gösterir.
 */
export function QuestionsTab({ examId, onChanged }: QuestionsTabProps) {
  const load = useCallback((signal: AbortSignal) => listQuestions(examId, signal), [examId])
  const { data, error, loading, reload } = useResource(load, 'Sorular yüklenemedi.')
  const showToast = useToast()
  const [draft, setDraft] = useState<Draft | null>(null)
  const [preview, setPreview] = useState<QuestionDetail | null>(null)
  const [removing, setRemoving] = useState<QuestionDetail | null>(null)
  const [pending, setPending] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const questions = [...(data ?? [])].sort((a, b) => a.order - b.order)

  const refresh = () => {
    reload()
    onChanged()
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!draft) return
    const options = draft.options.map((option) => option.trim())
    if (!draft.text.trim()) return setFormError('Soru metni zorunludur.')
    if (options.some((option) => !option)) return setFormError('Boş seçenek bırakmayın ya da silin.')
    setPending(true)
    setFormError(null)
    try {
      const body = { text: draft.text.trim(), options, correctOptionIndex: draft.correctOptionIndex }
      if (draft.id) await updateQuestion(draft.id, body)
      else await addQuestion(examId, body)
      showToast(draft.id ? 'Soru güncellendi.' : 'Soru eklendi.', 'success')
      setDraft(null)
      refresh()
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : 'Soru kaydedilemedi.')
    } finally {
      setPending(false)
    }
  }

  const remove = async () => {
    if (!removing) return
    setPending(true)
    try {
      await deleteQuestion(removing.id)
      showToast('Soru silindi.', 'success')
      setRemoving(null)
      refresh()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Soru silinemedi.', 'error')
      setRemoving(null)
    } finally {
      setPending(false)
    }
  }

  const setOption = (index: number, value: string) =>
    setDraft((current) => (current ? { ...current, options: current.options.map((option, i) => (i === index ? value : option)) } : current))

  const removeOption = (index: number) =>
    setDraft((current) => {
      if (!current || current.options.length <= MIN_OPTIONS) return current
      const options = current.options.filter((_, i) => i !== index)
      const correct = current.correctOptionIndex === index ? 0 : current.correctOptionIndex > index ? current.correctOptionIndex - 1 : current.correctOptionIndex
      return { ...current, options, correctOptionIndex: correct }
    })

  return (
    <div className="editor-card">
      <div className="editor-card-head">
        <div>
          <h2 className="editor-card-title">Sorular</h2>
          <p className="editor-card-sub">{data ? `${questions.length} soru · adaylara bu sırayla gösterilir` : 'Yükleniyor…'}</p>
        </div>
        <Button icon={<Plus size={18} aria-hidden="true" />} onClick={() => (setFormError(null), setDraft(emptyDraft()))}>
          Soru ekle
        </Button>
      </div>

      {error ? (
        <Alert tone="danger">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="question-bank">
          <Skeleton height="5rem" />
          <Skeleton height="5rem" />
        </div>
      ) : questions.length === 0 ? (
        <EmptyState icon={ListChecks} title="Henüz soru yok" action={<Button onClick={() => setDraft(emptyDraft())}>İlk soruyu ekle</Button>}>
          Her soru 2–10 seçenekli ve tek doğru cevaplı. Sorular eklendiği sırayla gösterilir.
        </EmptyState>
      ) : (
        <ol className="question-bank">
          <AnimatePresence initial={false}>
            {questions.map((question, index) => (
              <motion.li key={question.id} layout initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, height: 0 }} className="bank-item">
                <span className="bank-number">{index + 1}</span>
                <div className="bank-main">
                  <p className="bank-text">{question.text}</p>
                  <p className="bank-answer">
                    Doğru cevap: <strong>{LETTERS[question.correctOptionIndex]}</strong> · {question.options[question.correctOptionIndex]}
                    <span className="bank-count"> · {question.options.length} seçenek</span>
                  </p>
                </div>
                <div className="bank-actions">
                  <Button variant="ghost" aria-label={`${index + 1}. soruyu aday görünümünde aç`} onClick={() => setPreview(question)}>
                    <Eye size={18} aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    aria-label={`${index + 1}. soruyu düzenle`}
                    onClick={() => (setFormError(null), setDraft({ id: question.id, text: question.text, options: [...question.options], correctOptionIndex: question.correctOptionIndex }))}
                  >
                    <Pencil size={18} aria-hidden="true" />
                  </Button>
                  <Button variant="ghost" aria-label={`${index + 1}. soruyu sil`} onClick={() => setRemoving(question)}>
                    <Trash2 size={18} aria-hidden="true" />
                  </Button>
                </div>
              </motion.li>
            ))}
          </AnimatePresence>
        </ol>
      )}

      <Modal open={draft !== null} title={draft?.id ? 'Soruyu düzenle' : 'Yeni soru'} onClose={() => setDraft(null)}>
        {draft ? (
          <form className="question-form" onSubmit={save} noValidate>
            <label className="field">
              <span className="field-label">Soru metni</span>
              <textarea rows={3} maxLength={2000} value={draft.text} onChange={(event) => setDraft({ ...draft, text: event.target.value })} />
            </label>
            <fieldset className="option-list">
              <legend className="field-label">Seçenekler · doğru olanı işaretleyin</legend>
              {draft.options.map((option, index) => (
                <div key={index} className={['option-row', draft.correctOptionIndex === index && 'is-correct'].filter(Boolean).join(' ')}>
                  <label className="option-correct">
                    <input type="radio" name="correct" checked={draft.correctOptionIndex === index} onChange={() => setDraft({ ...draft, correctOptionIndex: index })} />
                    <OpticalBubble size="sm" filled={draft.correctOptionIndex === index}>
                      {LETTERS[index]}
                    </OpticalBubble>
                    <span className="visually-hidden">{LETTERS[index]} doğru cevap</span>
                  </label>
                  <input className="option-input" aria-label={`${LETTERS[index]} seçeneği`} maxLength={500} value={option} onChange={(event) => setOption(index, event.target.value)} />
                  <button type="button" className="option-remove" aria-label={`${LETTERS[index]} seçeneğini sil`} disabled={draft.options.length <= MIN_OPTIONS} onClick={() => removeOption(index)}>
                    <X size={16} aria-hidden="true" />
                  </button>
                </div>
              ))}
              {draft.options.length < MAX_OPTIONS ? (
                <Button variant="ghost" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setDraft({ ...draft, options: [...draft.options, ''] })}>
                  Seçenek ekle
                </Button>
              ) : null}
            </fieldset>
            {formError ? <Alert tone="danger">{formError}</Alert> : null}
            <div className="modal-actions">
              <Button variant="secondary" onClick={() => setDraft(null)}>
                Vazgeç
              </Button>
              <Button type="submit" loading={pending}>
                {draft.id ? 'Değişiklikleri kaydet' : 'Soruyu ekle'}
              </Button>
            </div>
          </form>
        ) : null}
      </Modal>

      <Modal open={preview !== null} title="Aday görünümü" onClose={() => setPreview(null)}>
        {preview ? (
          <div className="question-preview">
            <p className="question-preview-text">{preview.text}</p>
            <ul>
              {preview.options.map((option, index) => (
                <li key={index}>
                  <OpticalBubble size="sm">{LETTERS[index]}</OpticalBubble>
                  {option}
                </li>
              ))}
            </ul>
            <p className="form-note">Adayın ekranında doğru cevap görünmez.</p>
          </div>
        ) : null}
      </Modal>

      <Modal open={removing !== null} title="Soru silinsin mi?" onClose={() => setRemoving(null)}>
        <p>Bu soru sınavdan çıkarılacak. Oturum başlamış sınavda sunucu silmeye izin vermeyebilir.</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setRemoving(null)}>
            Vazgeç
          </Button>
          <Button variant="danger" loading={pending} onClick={() => void remove()}>
            Soruyu sil
          </Button>
        </div>
      </Modal>
    </div>
  )
}
