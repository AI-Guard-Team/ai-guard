import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Modal } from '../../components/Modal'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'

const messages = defineMessages({
  tr: {
    title: 'Sınavı teslim et',
    answered: 'Cevaplanan',
    empty: 'Boş',
    marked: 'İşaretli',
    emptyWarning: (n: number) => `${n} soru boş. Teslim ettikten sonra cevap değiştiremezsiniz.`,
    final: 'Teslim ettikten sonra cevap değiştiremezsiniz.',
    queued: (n: number) => `Gönderilmeyi bekleyen ${n} cevap var; teslimden önce gönderilecek. Bağlantı yoksa teslim tamamlanmaz.`,
    submitting: 'Teslim ediliyor…',
    submit: 'Teslim et',
  },
  en: {
    title: 'Submit exam',
    answered: 'Answered',
    empty: 'Blank',
    marked: 'Marked',
    emptyWarning: (n: number) => `${n} ${n === 1 ? 'question is' : 'questions are'} blank. You cannot change answers after submitting.`,
    final: 'You cannot change answers after submitting.',
    queued: (n: number) => `${n} ${n === 1 ? 'answer is' : 'answers are'} waiting to be sent and will be sent before submission. Without a connection, submission cannot complete.`,
    submitting: 'Submitting…',
    submit: 'Submit',
  },
})

interface SubmitDialogProps {
  open: boolean
  total: number
  answered: number
  marked: number
  queued: number
  pending: boolean
  error: string | null
  onConfirm: () => void
  onClose: () => void
}

/** Teslim onayı: boş ve işaretli sorular ile gönderilmeyi bekleyen cevaplar söylenir. */
export function SubmitDialog({ open, total, answered, marked, queued, pending, error, onConfirm, onClose }: SubmitDialogProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const empty = total - answered
  return (
    <Modal open={open} title={m.title} onClose={onClose}>
      <dl className="submit-summary">
        <div>
          <dt>{m.answered}</dt>
          <dd className="numeric">
            {answered} / {total}
          </dd>
        </div>
        <div>
          <dt>{m.empty}</dt>
          <dd className="numeric">{empty}</dd>
        </div>
        <div>
          <dt>{m.marked}</dt>
          <dd className="numeric">{marked}</dd>
        </div>
      </dl>
      {empty > 0 ? <p>{m.emptyWarning(empty)}</p> : <p>{m.final}</p>}
      {queued > 0 ? (
        <Alert tone="warning">{m.queued(queued)}</Alert>
      ) : null}
      {error ? <Alert tone="danger">{error}</Alert> : null}
      <div className="modal-actions">
        <Button variant="secondary" disabled={pending} onClick={onClose}>
          {c.cancel}
        </Button>
        <Button loading={pending} onClick={onConfirm}>
          {pending ? m.submitting : m.submit}
        </Button>
      </div>
    </Modal>
  )
}
