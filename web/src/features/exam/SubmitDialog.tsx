import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { Modal } from '../../components/Modal'

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
  const empty = total - answered
  return (
    <Modal open={open} title="Sınavı teslim et" onClose={onClose}>
      <dl className="submit-summary">
        <div>
          <dt>Cevaplanan</dt>
          <dd className="numeric">
            {answered} / {total}
          </dd>
        </div>
        <div>
          <dt>Boş</dt>
          <dd className="numeric">{empty}</dd>
        </div>
        <div>
          <dt>İşaretli</dt>
          <dd className="numeric">{marked}</dd>
        </div>
      </dl>
      {empty > 0 ? <p>{empty} soru boş. Teslim ettikten sonra cevap değiştiremezsiniz.</p> : <p>Teslim ettikten sonra cevap değiştiremezsiniz.</p>}
      {queued > 0 ? (
        <Alert tone="warning">Gönderilmeyi bekleyen {queued} cevap var; teslimden önce gönderilecek. Bağlantı yoksa teslim tamamlanmaz.</Alert>
      ) : null}
      {error ? <Alert tone="danger">{error}</Alert> : null}
      <div className="modal-actions">
        <Button variant="secondary" disabled={pending} onClick={onClose}>
          Vazgeç
        </Button>
        <Button loading={pending} onClick={onConfirm}>
          {pending ? 'Teslim ediliyor…' : 'Teslim et'}
        </Button>
      </div>
    </Modal>
  )
}
