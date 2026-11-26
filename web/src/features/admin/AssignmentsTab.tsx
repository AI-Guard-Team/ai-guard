import { Search, UserMinus, UserPlus, Users } from 'lucide-react'
import { useCallback, useDeferredValue, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { addAssignment, deleteAssignment, listUsers, type AssignmentDetail, type AssignmentRole } from '../../api/admin'
import { Button } from '../../components/Button'
import { StatusBadge } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'

const messages = defineMessages({
  tr: {
    sections: {
      Candidate: { title: 'Adaylar', empty: 'Henüz aday atanmadı.', hint: 'Sınava girecek kişiler; her adayın tek oturum hakkı var.' },
      Reviewer: { title: 'Değerlendiriciler', empty: 'Henüz değerlendirici atanmadı.', hint: 'Riskli ve işaretli oturumları inceleyip karar verecek kişiler.' },
    } as Record<AssignmentRole, { title: string; empty: string; hint: string }>,
    usersFailed: 'Kullanıcılar yüklenemedi.',
    addFailed: 'Atama yapılamadı.',
    removeFailed: 'Atama kaldırılamadı.',
    searchLabel: (title: string) => `${title} arasına eklemek için ara`,
    searchPlaceholder: 'Ad ya da e-postayla ara',
    noMatch: 'Eşleşen, atanmamış aktif kullanıcı yok.',
    assign: 'Ata',
    used: (at: string) => `Hakkını kullandı · ${at}`,
    remove: (name: string) => `${name} atamasını kaldır`,
  },
  en: {
    sections: {
      Candidate: { title: 'Candidates', empty: 'No candidates assigned yet.', hint: 'People who will take the exam; each candidate has one attempt.' },
      Reviewer: { title: 'Reviewers', empty: 'No reviewers assigned yet.', hint: 'People who will review risky and flagged sessions and decide.' },
    },
    usersFailed: 'Users could not be loaded.',
    addFailed: 'The assignment could not be made.',
    removeFailed: 'The assignment could not be removed.',
    searchLabel: (title: string) => `Search to add to ${title.toLowerCase()}`,
    searchPlaceholder: 'Search by name or email',
    noMatch: 'No matching active user who is not already assigned.',
    assign: 'Assign',
    used: (at: string) => `Attempt used · ${at}`,
    remove: (name: string) => `Remove ${name}’s assignment`,
  },
})

interface AssignmentsTabProps {
  examId: string
  assignments: AssignmentDetail[]
  onChanged: () => void
}

const ROLES: AssignmentRole[] = ['Candidate', 'Reviewer']

/** Atamalar (Stitch 28): adaylar ve değerlendiriciler ayrı; aktif kullanıcılar arasından aranarak eklenir. */
export function AssignmentsTab({ examId, assignments, onChanged }: AssignmentsTabProps) {
  const m = useMessages(messages)
  return (
    <div className="assign-grid">
      {ROLES.map((role) => (
        <AssignmentSection key={role} examId={examId} role={role} {...m.sections[role]} rows={assignments.filter((row) => row.role === role)} onChanged={onChanged} />
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
  const m = useMessages(messages)
  const load = useCallback((signal: AbortSignal) => listUsers(role, signal), [role])
  const { data: users } = useResource(load, m.usersFailed)
  const showToast = useToast()
  const [query, setQuery] = useState('')
  const [busy, setBusy] = useState<string | null>(null)

  const assigned = useMemo(() => new Set(rows.map((row) => row.userId)), [rows])
  const locale = getLocale()
  const term = useDeferredValue(query).trim().toLocaleLowerCase(locale)
  const matches = term
    ? (users ?? [])
        .filter((user) => user.isActive && !assigned.has(user.id))
        .filter((user) => user.fullName.toLocaleLowerCase(locale).includes(term) || user.email.toLocaleLowerCase(locale).includes(term))
        .slice(0, 6)
    : []

  const add = async (userId: string) => {
    if (busy) return
    setBusy(userId)
    try {
      await addAssignment(examId, userId, role)
      setQuery('')
      onChanged()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.addFailed, 'error')
    } finally {
      setBusy(null)
    }
  }

  const remove = async (row: AssignmentDetail) => {
    if (busy) return
    setBusy(row.id)
    try {
      await deleteAssignment(row.id)
      onChanged()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.removeFailed, 'error')
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
          <span className="visually-hidden">{m.searchLabel(title)}</span>
          <input type="search" placeholder={m.searchPlaceholder} maxLength={200} value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
        {term ? (
          <ul className="assign-results">
            {matches.length === 0 ? (
              <li className="assign-results-empty">{m.noMatch}</li>
            ) : (
              matches.map((user) => (
                <li key={user.id}>
                  <span>
                    <strong>{user.fullName}</strong>
                    <span className="admin-row-sub">{user.email}</span>
                  </span>
                  <Button variant="secondary" loading={busy === user.id} icon={<UserPlus size={16} aria-hidden="true" />} onClick={() => void add(user.id)}>
                    {m.assign}
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
                {row.fullName.slice(0, 1).toLocaleUpperCase(locale)}
              </span>
              <span className="assign-person">
                <strong>{row.fullName}</strong>
                <span className="admin-row-sub">{row.email}</span>
              </span>
              {row.attemptUsedAt ? <StatusBadge tone="info">{m.used(formatDateTime(row.attemptUsedAt))}</StatusBadge> : null}
              <Button variant="ghost" aria-label={m.remove(row.fullName)} loading={busy === row.id} onClick={() => void remove(row)}>
                <UserMinus size={18} aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
