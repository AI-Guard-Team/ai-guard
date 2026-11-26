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
import { common } from '../../i18n/common'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    failed: 'Sorular yüklenemedi.',
    textRequired: 'Soru metni zorunludur.',
    emptyOption: 'Boş seçenek bırakmayın ya da silin.',
    duplicate: 'Aynı seçenek iki kez yazılmış; her seçenek farklı olmalı.',
    updated: 'Soru güncellendi.',
    added: 'Soru eklendi.',
    saveFailed: 'Soru kaydedilemedi.',
    deleted: 'Soru silindi.',
    deleteFailed: 'Soru silinemedi.',
    title: 'Sorular',
    count: (n: number) => `${n} soru · adaylara bu sırayla gösterilir`,
    add: 'Soru ekle',
    emptyTitle: 'Henüz soru yok',
    first: 'İlk soruyu ekle',
    emptyText: 'Her soru 2–10 seçenekli ve tek doğru cevaplı. Sorular eklendiği sırayla gösterilir.',
    correct: 'Doğru cevap:',
    options: (n: number) => ` · ${n} seçenek`,
    previewLabel: (n: number) => `${n}. soruyu aday görünümünde aç`,
    editLabel: (n: number) => `${n}. soruyu düzenle`,
    deleteLabel: (n: number) => `${n}. soruyu sil`,
    editTitle: 'Soruyu düzenle',
    newTitle: 'Yeni soru',
    text: 'Soru metni',
    optionsLegend: 'Seçenekler · doğru olanı işaretleyin',
    correctLabel: (letter: string) => `${letter} doğru cevap`,
    optionLabel: (letter: string) => `${letter} seçeneği`,
    removeOption: (letter: string) => `${letter} seçeneğini sil`,
    addOption: 'Seçenek ekle',
    saveChanges: 'Değişiklikleri kaydet',
    addQuestion: 'Soruyu ekle',
    preview: 'Aday görünümü',
    previewNote: 'Adayın ekranında doğru cevap görünmez.',
    confirmTitle: 'Soru silinsin mi?',
    confirmText: 'Bu soru sınavdan çıkarılacak. Oturum başlamış sınavda sunucu silmeye izin vermeyebilir.',
    deleteQuestion: 'Soruyu sil',
  },
  en: {
    failed: 'Questions could not be loaded.',
    textRequired: 'Question text is required.',
    emptyOption: 'Do not leave an option blank; fill it in or remove it.',
    duplicate: 'The same option appears twice; every option must be different.',
    updated: 'Question updated.',
    added: 'Question added.',
    saveFailed: 'The question could not be saved.',
    deleted: 'Question deleted.',
    deleteFailed: 'The question could not be deleted.',
    title: 'Questions',
    count: (n: number) => `${n} ${n === 1 ? 'question' : 'questions'} · shown to candidates in this order`,
    add: 'Add question',
    emptyTitle: 'No questions yet',
    first: 'Add the first question',
    emptyText: 'Each question has 2–10 options and one correct answer. Questions are shown in the order they are added.',
    correct: 'Correct answer:',
    options: (n: number) => ` · ${n} options`,
    previewLabel: (n: number) => `Open question ${n} in candidate view`,
    editLabel: (n: number) => `Edit question ${n}`,
    deleteLabel: (n: number) => `Delete question ${n}`,
    editTitle: 'Edit question',
    newTitle: 'New question',
    text: 'Question text',
    optionsLegend: 'Options · mark the correct one',
    correctLabel: (letter: string) => `${letter} is the correct answer`,
    optionLabel: (letter: string) => `Option ${letter}`,
    removeOption: (letter: string) => `Remove option ${letter}`,
    addOption: 'Add option',
    saveChanges: 'Save changes',
    addQuestion: 'Add question',
    preview: 'Candidate view',
    previewNote: 'The correct answer is not shown on the candidate’s screen.',
    confirmTitle: 'Delete this question?',
    confirmText: 'This question will be removed from the exam. The server may refuse if sessions have already started.',
    deleteQuestion: 'Delete question',
  },
})

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
  const m = useMessages(messages)
  const c = useMessages(common)
  const load = useCallback((signal: AbortSignal) => listQuestions(examId, signal), [examId])
  const { data, error, loading, reload } = useResource(load, m.failed)
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
    if (!draft || pending) return
    const options = draft.options.map((option) => option.trim())
    if (!draft.text.trim()) return setFormError(m.textRequired)
    if (options.some((option) => !option)) return setFormError(m.emptyOption)
    const normalized = options.map((option) => option.toLocaleLowerCase(getLocale()))
    if (new Set(normalized).size !== normalized.length) return setFormError(m.duplicate)
    setPending(true)
    setFormError(null)
    try {
      const body = { text: draft.text.trim(), options, correctOptionIndex: draft.correctOptionIndex }
      if (draft.id) await updateQuestion(draft.id, body)
      else await addQuestion(examId, body)
      showToast(draft.id ? m.updated : m.added, 'success')
      setDraft(null)
      refresh()
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : m.saveFailed)
    } finally {
      setPending(false)
    }
  }

  const remove = async () => {
    if (!removing) return
    setPending(true)
    try {
      await deleteQuestion(removing.id)
      showToast(m.deleted, 'success')
      setRemoving(null)
      refresh()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.deleteFailed, 'error')
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
          <h2 className="editor-card-title">{m.title}</h2>
          <p className="editor-card-sub">{data ? m.count(questions.length) : c.loading}</p>
        </div>
        <Button icon={<Plus size={18} aria-hidden="true" />} onClick={() => (setFormError(null), setDraft(emptyDraft()))}>
          {m.add}
        </Button>
      </div>

      {error ? (
        <Alert tone="danger">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <div className="question-bank">
          <Skeleton height="5rem" />
          <Skeleton height="5rem" />
        </div>
      ) : questions.length === 0 ? (
        <EmptyState icon={ListChecks} title={m.emptyTitle} action={<Button onClick={() => setDraft(emptyDraft())}>{m.first}</Button>}>
          {m.emptyText}
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
                    {m.correct} <strong>{LETTERS[question.correctOptionIndex]}</strong> · {question.options[question.correctOptionIndex]}
                    <span className="bank-count">{m.options(question.options.length)}</span>
                  </p>
                </div>
                <div className="bank-actions">
                  <Button variant="ghost" aria-label={m.previewLabel(index + 1)} onClick={() => setPreview(question)}>
                    <Eye size={18} aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    aria-label={m.editLabel(index + 1)}
                    onClick={() => (setFormError(null), setDraft({ id: question.id, text: question.text, options: [...question.options], correctOptionIndex: question.correctOptionIndex }))}
                  >
                    <Pencil size={18} aria-hidden="true" />
                  </Button>
                  <Button variant="ghost" aria-label={m.deleteLabel(index + 1)} onClick={() => setRemoving(question)}>
                    <Trash2 size={18} aria-hidden="true" />
                  </Button>
                </div>
              </motion.li>
            ))}
          </AnimatePresence>
        </ol>
      )}

      <Modal open={draft !== null} title={draft?.id ? m.editTitle : m.newTitle} onClose={() => setDraft(null)}>
        {draft ? (
          <form className="question-form" onSubmit={save} noValidate>
            <label className="field">
              <span className="field-label">{m.text}</span>
              <textarea rows={3} maxLength={2000} value={draft.text} onChange={(event) => setDraft({ ...draft, text: event.target.value })} />
            </label>
            <fieldset className="option-list">
              <legend className="field-label">{m.optionsLegend}</legend>
              {draft.options.map((option, index) => (
                <div key={index} className={['option-row', draft.correctOptionIndex === index && 'is-correct'].filter(Boolean).join(' ')}>
                  <label className="option-correct">
                    <input type="radio" name="correct" checked={draft.correctOptionIndex === index} onChange={() => setDraft({ ...draft, correctOptionIndex: index })} />
                    <OpticalBubble size="sm" filled={draft.correctOptionIndex === index}>
                      {LETTERS[index]}
                    </OpticalBubble>
                    <span className="visually-hidden">{m.correctLabel(LETTERS[index])}</span>
                  </label>
                  <input className="option-input" aria-label={m.optionLabel(LETTERS[index])} maxLength={500} value={option} onChange={(event) => setOption(index, event.target.value)} />
                  <button type="button" className="option-remove" aria-label={m.removeOption(LETTERS[index])} disabled={draft.options.length <= MIN_OPTIONS} onClick={() => removeOption(index)}>
                    <X size={16} aria-hidden="true" />
                  </button>
                </div>
              ))}
              {draft.options.length < MAX_OPTIONS ? (
                <Button variant="ghost" icon={<Plus size={16} aria-hidden="true" />} onClick={() => setDraft({ ...draft, options: [...draft.options, ''] })}>
                  {m.addOption}
                </Button>
              ) : null}
            </fieldset>
            {formError ? <Alert tone="danger">{formError}</Alert> : null}
            <div className="modal-actions">
              <Button variant="secondary" onClick={() => setDraft(null)}>
                {c.cancel}
              </Button>
              <Button type="submit" loading={pending}>
                {draft.id ? m.saveChanges : m.addQuestion}
              </Button>
            </div>
          </form>
        ) : null}
      </Modal>

      <Modal open={preview !== null} title={m.preview} onClose={() => setPreview(null)}>
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
            <p className="form-note">{m.previewNote}</p>
          </div>
        ) : null}
      </Modal>

      <Modal open={removing !== null} title={m.confirmTitle} onClose={() => setRemoving(null)}>
        <p>{m.confirmText}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setRemoving(null)}>
            {c.cancel}
          </Button>
          <Button variant="danger" loading={pending} onClick={() => void remove()}>
            {m.deleteQuestion}
          </Button>
        </div>
      </Modal>
    </div>
  )
}
