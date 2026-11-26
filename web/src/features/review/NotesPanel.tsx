import { useState, type FormEvent } from 'react'
import { addNote, type ReviewNote } from '../../api/review'
import { ApiError } from '../../api/client'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { formatDateTime } from '../../lib/format'

const MAX_NOTE = 2000

interface NotesPanelProps {
  sessionId: string
  notes: ReviewNote[]
  canWrite: boolean
  onAdded: () => void
}

/** Değerlendirici notları (K-31): çizgili defter kâğıdı. Notlar silinmez, yalnızca eklenir. */
export function NotesPanel({ sessionId, notes, canWrite, onAdded }: NotesPanelProps) {
  const [text, setText] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!text.trim()) return
    setPending(true)
    setError(null)
    try {
      await addNote(sessionId, text.trim())
      setText('')
      onAdded()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Not eklenemedi.')
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="notes">
      {notes.length === 0 ? <p className="notes-empty">Henüz not yok.</p> : null}
      <ol className="notes-list">
        {notes.map((note) => (
          <li key={note.id}>
            <p className="kicker">
              {note.authorName} · {formatDateTime(note.createdAt)}
            </p>
            <p className="notes-text">{note.text}</p>
          </li>
        ))}
      </ol>
      {canWrite ? (
        <form className="notes-form" onSubmit={submit}>
          <label className="kicker" htmlFor={`note-${sessionId}`}>
            Not ekle
          </label>
          <textarea
            id={`note-${sessionId}`}
            className="lined-paper"
            rows={4}
            maxLength={MAX_NOTE}
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
          {error ? <Alert tone="danger">{error}</Alert> : null}
          <div className="notes-actions">
            <span className="kicker numeric">
              {text.length}/{MAX_NOTE}
            </span>
            <Button type="submit" variant="secondary" loading={pending} disabled={!text.trim()}>
              Notu kaydet
            </Button>
          </div>
        </form>
      ) : null}
    </div>
  )
}
