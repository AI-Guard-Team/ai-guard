import { Search, UserMinus, UserPlus, Users } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { addAssignment, deleteAssignment, listUsers, type AssignmentDetail, type AssignmentRole } from '../../api/admin'
import { Button } from '../../components/Button'
import { StatusBadge } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { formatDateTime } from '../../lib/format'

interface AssignmentsTabProps {
  examId: string
  assignments: AssignmentDetail[]
  onChanged: () => void
}

const SECTIONS: { role: AssignmentRole; title: string; empty: string; hint: string }[] = [
  { role: 'Candidate', title: 'Adaylar', empty: 'Henüz aday atanmadı.', hint: 'Sınava girecek kişiler; her adayın tek oturum hakkı var.' },
  { role: 'Reviewer', title: 'Değerlendiriciler', empty: 'Henüz değerlendirici atanmadı.', hint: 'Riskli ve işaretli oturumları inceleyip karar verecek kişiler.' },
]

/** Atamalar (Stitch 28): adaylar ve değerlendiriciler ayrı; aktif kullanıcılar arasından aranarak eklenir. */
export function AssignmentsTab({ examId, assignments, onChanged }: AssignmentsTabProps) {
  return (
    <div className="assign-grid">
      {SECTIONS.map((section) => (
        <AssignmentSection key={section.role} examId={examId} {...section} rows={assignments.filter((row) => row.role === section.role)} onChanged={onChanged} />
      ))}
    </div>
  )
}

interface SectionProps {
  examId: string
  role: AssignmentRole
  title: string
  empty: string
  hint: string
  rows: AssignmentDetail[]
  onChanged: () => void
}

function AssignmentSection({ examId, role, title, empty, hint, rows, onChanged }: SectionProps) {
  const load = useCallback((signal: AbortSignal) => listUsers(role, signal), [role])
  const { data: users } = useResource(load, 'Kullanıcılar yüklenemedi.')
  const showToast = useToast()
  const [query, setQuery] = useState('')
  const [busy, setBusy] = useState<string | null>(null)

  const assigned = useMemo(() => new Set(rows.map((row) => row.userId)), [rows])
  const term = query.trim().toLocaleLowerCase('tr-TR')
  const matches = term
    ? (users ?? [])
        .filter((user) => user.isActive && !assigned.has(user.id))
        .filter((user) => user.fullName.toLocaleLowerCase('tr-TR').includes(term) || user.email.toLocaleLowerCase('tr-TR').includes(term))
        .slice(0, 6)
    : []

  const add = async (userId: string) => {
    setBusy(userId)
    try {
      await addAssignment(examId, userId, role)
      setQuery('')
      onChanged()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Atama yapılamadı.', 'error')
    } finally {
      setBusy(null)
    }
  }

  const remove = async (row: AssignmentDetail) => {
    setBusy(row.id)
    try {
      await deleteAssignment(row.id)
      onChanged()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Atama kaldırılamadı.', 'error')
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className="editor-card assign-card" aria-labelledby={`assign-${role}`}>
      <div className="editor-card-head">
        <div>
          <h2 className="editor-card-title" id={`assign-${role}`}>
            {title} <span className="count-pill">{rows.length}</span>
          </h2>
          <p className="editor-card-sub">{hint}</p>
        </div>
      </div>

      <div className="assign-search">
        <label className="search-field">
          <Search size={18} aria-hidden="true" />
          <span className="visually-hidden">{title} arasına eklemek için ara</span>
          <input type="search" placeholder="Ad ya da e-postayla ara" value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
        {term ? (
          <ul className="assign-results">
            {matches.length === 0 ? (
              <li className="assign-results-empty">Eşleşen, atanmamış aktif kullanıcı yok.</li>
            ) : (
              matches.map((user) => (
                <li key={user.id}>
                  <span>
                    <strong>{user.fullName}</strong>
                    <span className="admin-row-sub">{user.email}</span>
                  </span>
                  <Button variant="secondary" loading={busy === user.id} icon={<UserPlus size={16} aria-hidden="true" />} onClick={() => void add(user.id)}>
                    Ata
                  </Button>
                </li>
              ))
            )}
          </ul>
        ) : null}
      </div>

      {rows.length === 0 ? (
        <p className="assign-empty">
          <Users size={18} aria-hidden="true" />
          {empty}
        </p>
      ) : (
        <ul className="assign-list">
          {rows.map((row) => (
            <li key={row.id}>
              <span className="assign-avatar" aria-hidden="true">
                {row.fullName.slice(0, 1).toLocaleUpperCase('tr-TR')}
              </span>
              <span className="assign-person">
                <strong>{row.fullName}</strong>
                <span className="admin-row-sub">{row.email}</span>
              </span>
              {row.attemptUsedAt ? <StatusBadge tone="info">Hakkını kullandı · {formatDateTime(row.attemptUsedAt)}</StatusBadge> : null}
              <Button variant="ghost" aria-label={`${row.fullName} atamasını kaldır`} loading={busy === row.id} onClick={() => void remove(row)}>
                <UserMinus size={18} aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
