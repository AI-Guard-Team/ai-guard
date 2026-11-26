import { MailPlus, Search, UserRoundX, UserRoundCheck, Users } from 'lucide-react'
import { useDeferredValue, useMemo, useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { createUser, listUsers, updateUser, type InviteStatus, type UserRole, type UserSummary } from '../../api/admin'
import { ROLE_LABEL } from '../../auth/roles'
import { useAuth } from '../../auth/useAuth'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { Input } from '../../components/Input'
import { Modal } from '../../components/Modal'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge, type BadgeTone } from '../../components/StatusBadge'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { getLang, getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDate } from '../../lib/format'
import { MAX_EMAIL_LENGTH, validateEmail } from '../../pages/auth/passwordRules'
import './admin.css'

const ROLES: UserRole[] = ['Candidate', 'Reviewer', 'Admin']
const MIN_NAME = 2
const MAX_NAME = 200

const INVITE_TONE: Record<InviteStatus, BadgeTone> = { Accepted: 'success', Pending: 'info', Expired: 'warning' }

const messages = defineMessages({
  tr: {
    plural: { Candidate: 'Adaylar', Reviewer: 'Değerlendiriciler', Admin: 'Yöneticiler' } as Record<UserRole, string>,
    invite: { Accepted: 'Hesap etkin', Pending: 'Davet bekliyor', Expired: 'Davetin süresi doldu' } as Record<InviteStatus, string>,
    roleHint: {
      Candidate: 'Atandığı sınavlara girer',
      Reviewer: 'Dosyaları inceler, karar verir',
      Admin: 'Sınav, kullanıcı ve ayarları yönetir',
    } as Record<UserRole, string>,
    failed: 'Kullanıcılar yüklenemedi.',
    roleChanged: (name: string, role: string) => `${name} artık ${role}.`,
    roleFailed: 'Rol değiştirilemedi.',
    deactivated: 'Hesap pasife alındı.',
    reactivated: 'Hesap yeniden etkinleştirildi.',
    updateFailed: 'Hesap güncellenemedi.',
    admin: 'Yönetim',
    title: 'Kullanıcılar',
    description: 'Hesap açın, rolleri yönetin, gerekirse hesabı pasife alın.',
    info: [
      'Davet, kullanıcının e-posta adresine 72 saat geçerli bir şifre belirleme bağlantısı gönderir. Kullanıcı şifresini belirleyince hesap etkin olur.',
      'Bağlantının süresi dolarsa durum "Davetin süresi doldu" olur.',
      'Pasife alınan kullanıcı giriş yapamaz; kayıtları silinmez.',
      'Rol değişikliği ve pasife alma denetim kaydına yazılır.',
    ],
    inviteUser: 'Kullanıcı davet et',
    filterBy: 'Role göre süz',
    all: 'Tümü',
    search: 'Kullanıcı ara',
    searchPlaceholder: 'Ad ya da e-postayla ara',
    listFailed: 'Kullanıcılar yüklenemedi',
    noMatch: 'Eşleşen kullanıcı yok',
    none: 'Henüz kullanıcı yok',
    noMatchText: 'Aramayı ya da filtreyi değiştirin.',
    noneText: '"Kullanıcı davet et" ile ilk hesabı açın.',
    user: 'Kullanıcı',
    role: 'Rol',
    status: 'Durum',
    added: 'Eklenme',
    action: 'İşlem',
    roleOf: (name: string) => `${name} rolü`,
    inactive: 'Pasif',
    you: 'Siz',
    deactivate: 'Pasife al',
    activate: 'Etkinleştir',
    invited: (name: string) => `${name} için davet e-postası gönderildi.`,
    deactivateTitle: 'Hesap pasife alınsın mı?',
    activateTitle: 'Hesap etkinleştirilsin mi?',
    deactivateText: (name: string) =>
      `${name} giriş yapamayacak; açık oturumu en geç 15 dakika içinde kapanır. Kayıtları silinmez, istediğinizde yeniden etkinleştirebilirsiniz.`,
    activateText: (name: string) => `${name} yeniden giriş yapabilecek.`,
    nameRequired: 'Ad soyad zorunludur.',
    nameShort: `Ad soyad en az ${MIN_NAME} karakter olmalıdır.`,
    exists: 'Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.',
    createFailed: 'Kullanıcı oluşturulamadı.',
    inviteHow: (
      <>
        Kullanıcının e-posta adresine <strong>72 saat geçerli</strong> bir şifre belirleme bağlantısı gider. Kullanıcı şifresini belirleyince hesabı etkin olur.
      </>
    ),
    emailNote: 'Not: Gerçek e-posta gönderimi henüz kurulu değil; şimdilik bağlantı sunucu kaydına yazılıyor (S-15).',
    name: 'Ad soyad',
    email: 'E-posta',
    send: 'Davet gönder',
  },
  en: {
    plural: { Candidate: 'Candidates', Reviewer: 'Reviewers', Admin: 'Administrators' },
    invite: { Accepted: 'Account active', Pending: 'Invitation pending', Expired: 'Invitation expired' },
    roleHint: {
      Candidate: 'Takes the exams they are assigned',
      Reviewer: 'Reviews files and decides',
      Admin: 'Manages exams, users and settings',
    },
    failed: 'Users could not be loaded.',
    roleChanged: (name: string, role: string) => `${name} is now ${/^[aeiou]/i.test(role) ? 'an' : 'a'} ${role}.`,
    roleFailed: 'The role could not be changed.',
    deactivated: 'Account deactivated.',
    reactivated: 'Account reactivated.',
    updateFailed: 'The account could not be updated.',
    admin: 'Administration',
    title: 'Users',
    description: 'Open accounts, manage roles and deactivate accounts when needed.',
    info: [
      'An invitation sends the user a password setup link valid for 72 hours. The account becomes active once they set a password.',
      'If the link expires, the status becomes "Invitation expired".',
      'A deactivated user cannot sign in; their records are not deleted.',
      'Role changes and deactivations are written to the audit log.',
    ],
    inviteUser: 'Invite user',
    filterBy: 'Filter by role',
    all: 'All',
    search: 'Search users',
    searchPlaceholder: 'Search by name or email',
    listFailed: 'Users could not be loaded',
    noMatch: 'No matching users',
    none: 'No users yet',
    noMatchText: 'Change the search or the filter.',
    noneText: 'Open the first account with "Invite user".',
    user: 'User',
    role: 'Role',
    status: 'Status',
    added: 'Added',
    action: 'Action',
    roleOf: (name: string) => `${name}’s role`,
    inactive: 'Inactive',
    you: 'You',
    deactivate: 'Deactivate',
    activate: 'Activate',
    invited: (name: string) => `An invitation email was sent to ${name}.`,
    deactivateTitle: 'Deactivate this account?',
    activateTitle: 'Activate this account?',
    deactivateText: (name: string) =>
      `${name} will not be able to sign in; any open session ends within 15 minutes. Records are not deleted and you can reactivate the account at any time.`,
    activateText: (name: string) => `${name} will be able to sign in again.`,
    nameRequired: 'Full name is required.',
    nameShort: `Full name must be at least ${MIN_NAME} characters.`,
    exists: 'A user with this email address already exists.',
    createFailed: 'The user could not be created.',
    inviteHow: (
      <>
        The user receives a password setup link <strong>valid for 72 hours</strong>. Their account becomes active once they set a password.
      </>
    ),
    emailNote: 'Note: real email delivery is not set up yet; for now the link is written to the server log (S-15).',
    name: 'Full name',
    email: 'Email',
    send: 'Send invitation',
  },
})

const load = (signal: AbortSignal) => listUsers(undefined, signal)

/** Kullanıcılar (Stitch 23) ve davet (Stitch 24). Hesabı yönetici açar; kullanıcıya şifre belirleme bağlantısı gider (K-20). */
export function UsersPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { user: me } = useAuth()
  const { data, error, loading, reload } = useResource(load, m.failed)
  const showToast = useToast()
  const [role, setRole] = useState<UserRole | 'all'>('all')
  const [query, setQuery] = useState('')
  const deferredQuery = useDeferredValue(query)
  const [inviting, setInviting] = useState(false)
  const [toggling, setToggling] = useState<UserSummary | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const locale = getLocale()

  const rows = useMemo(() => {
    const term = deferredQuery.trim().toLocaleLowerCase(locale)
    return (data ?? [])
      .filter((user) => role === 'all' || user.role === role)
      .filter((user) => !term || user.fullName.toLocaleLowerCase(locale).includes(term) || user.email.toLocaleLowerCase(locale).includes(term))
      .sort((a, b) => a.fullName.localeCompare(b.fullName, getLang()))
  }, [data, role, deferredQuery, locale])

  const counts = useMemo(() => {
    const result: Record<UserRole | 'all', number> = { all: data?.length ?? 0, Candidate: 0, Reviewer: 0, Admin: 0 }
    for (const user of data ?? []) result[user.role] += 1
    return result
  }, [data])

  const changeRole = async (user: UserSummary, next: UserRole) => {
    if (busy) return
    setBusy(user.id)
    try {
      await updateUser(user.id, { role: next })
      showToast(m.roleChanged(user.fullName, ROLE_LABEL[next].toLocaleLowerCase(locale)), 'success')
      reload()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.roleFailed, 'error')
    } finally {
      setBusy(null)
    }
  }

  const toggleActive = async () => {
    if (!toggling || busy) return
    setBusy(toggling.id)
    try {
      await updateUser(toggling.id, { isActive: !toggling.isActive })
      showToast(toggling.isActive ? m.deactivated : m.reactivated, 'success')
      setToggling(null)
      reload()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : m.updateFailed, 'error')
      setToggling(null)
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: m.admin }]}
        title={m.title}
        description={m.description}
        info={
          <ul>
            {m.info.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        }
        actions={
          <Button icon={<MailPlus size={18} aria-hidden="true" />} onClick={() => setInviting(true)}>
            {m.inviteUser}
          </Button>
        }
      />

      <div className="admin-toolbar">
        <div className="chip-tabs" role="group" aria-label={m.filterBy}>
          {(['all', ...ROLES] as const).map((key) => (
            <button key={key} type="button" className="chip-tab" aria-pressed={role === key} onClick={() => setRole(key)}>
              {key === 'all' ? m.all : m.plural[key]} <span className="chip-count">{counts[key]}</span>
            </button>
          ))}
        </div>
        <label className="search-field">
          <Search size={18} aria-hidden="true" />
          <span className="visually-hidden">{m.search}</span>
          <input type="search" placeholder={m.searchPlaceholder} maxLength={200} value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title={m.listFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <Skeleton height="20rem" radius="lg" />
      ) : rows.length === 0 ? (
        <EmptyState icon={Users} title={data?.length ? m.noMatch : m.none}>
          {data?.length ? m.noMatchText : m.noneText}
        </EmptyState>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">{m.user}</th>
                <th scope="col">{m.role}</th>
                <th scope="col">{m.status}</th>
                <th scope="col">{m.added}</th>
                <th scope="col">
                  <span className="visually-hidden">{m.action}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((user) => {
                const self = user.id === me?.id
                return (
                  <tr key={user.id} className={user.isActive ? undefined : 'is-muted'}>
                    <td>
                      <span className="person">
                        <span className="assign-avatar" aria-hidden="true">
                          {user.fullName.slice(0, 1).toLocaleUpperCase(locale)}
                        </span>
                        <span className="assign-person">
                          <strong>{user.fullName}</strong>
                          <span className="admin-row-sub">{user.email}</span>
                        </span>
                      </span>
                    </td>
                    <td>
                      <select
                        className="select select-sm"
                        aria-label={m.roleOf(user.fullName)}
                        value={user.role}
                        disabled={self || busy === user.id}
                        onChange={(event) => void changeRole(user, event.target.value as UserRole)}
                      >
                        {ROLES.map((item) => (
                          <option key={item} value={item}>
                            {ROLE_LABEL[item]}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      {user.isActive ? (
                        <StatusBadge tone={INVITE_TONE[user.inviteStatus]} dot>
                          {m.invite[user.inviteStatus]}
                        </StatusBadge>
                      ) : (
                        <StatusBadge tone="neutral" dot>
                          {m.inactive}
                        </StatusBadge>
                      )}
                    </td>
                    <td className="numeric">{formatDate(user.createdAt)}</td>
                    <td className="cell-actions">
                      {self ? (
                        <span className="admin-row-sub">{m.you}</span>
                      ) : (
                        <Button
                          variant="ghost"
                          icon={user.isActive ? <UserRoundX size={16} aria-hidden="true" /> : <UserRoundCheck size={16} aria-hidden="true" />}
                          onClick={() => setToggling(user)}
                        >
                          {user.isActive ? m.deactivate : m.activate}
                        </Button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      <InviteDialog
        open={inviting}
        onClose={() => setInviting(false)}
        onInvited={(name) => {
          setInviting(false)
          showToast(m.invited(name), 'success')
          reload()
        }}
      />

      <Modal open={toggling !== null} title={toggling?.isActive ? m.deactivateTitle : m.activateTitle} onClose={() => setToggling(null)}>
        <p>{toggling?.isActive ? m.deactivateText(toggling.fullName) : m.activateText(toggling?.fullName ?? '')}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setToggling(null)}>
            {c.cancel}
          </Button>
          <Button variant={toggling?.isActive ? 'danger' : 'primary'} loading={busy === toggling?.id} onClick={() => void toggleActive()}>
            {toggling?.isActive ? m.deactivate : m.activate}
          </Button>
        </div>
      </Modal>
    </div>
  )
}

interface InviteDialogProps {
  open: boolean
  onClose: () => void
  onInvited: (name: string) => void
}

/** Kullanıcı oluştur ve davet et (Stitch 24). */
function InviteDialog({ open, onClose, onInvited }: InviteDialogProps) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>('Candidate')
  const [errors, setErrors] = useState<{ fullName?: string; email?: string }>({})
  const [message, setMessage] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  const reset = () => {
    setFullName('')
    setEmail('')
    setRole('Candidate')
    setErrors({})
    setMessage(null)
  }

  const validateName = (value: string) => (!value.trim() ? m.nameRequired : value.trim().length < MIN_NAME ? m.nameShort : undefined)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (pending) return
    const next = { fullName: validateName(fullName), email: validateEmail(email) }
    setErrors(next)
    if (next.fullName || next.email) return
    setPending(true)
    setMessage(null)
    try {
      await createUser({ fullName: fullName.trim().replace(/\s+/g, ' '), email: email.trim(), role })
      onInvited(fullName.trim())
      reset()
    } catch (caught) {
      // 409: aynı e-posta. Alanın altında söylenir; kullanıcı neyi düzelteceğini görür.
      if (caught instanceof ApiError && caught.status === 409) setErrors((current) => ({ ...current, email: m.exists }))
      else setMessage(caught instanceof ApiError ? caught.message : m.createFailed)
    } finally {
      setPending(false)
    }
  }

  return (
    <Modal open={open} title={m.inviteUser} onClose={() => (reset(), onClose())}>
      <form onSubmit={submit} noValidate>
        <div className="invite-how">
          <MailPlus size={18} aria-hidden="true" />
          <p>{m.inviteHow}</p>
        </div>
        <p className="form-note">{m.emailNote}</p>
        <Input
          label={m.name}
          value={fullName}
          error={errors.fullName}
          maxLength={MAX_NAME}
          autoComplete="off"
          onChange={(event) => {
            setFullName(event.target.value)
            if (errors.fullName) setErrors((current) => ({ ...current, fullName: validateName(event.target.value) }))
          }}
        />
        <Input
          label={m.email}
          type="email"
          inputMode="email"
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          maxLength={MAX_EMAIL_LENGTH}
          value={email}
          error={errors.email}
          onChange={(event) => {
            setEmail(event.target.value)
            if (errors.email) setErrors((current) => ({ ...current, email: validateEmail(event.target.value) }))
          }}
          onBlur={() => {
            if (email) setErrors((current) => ({ ...current, email: validateEmail(email) }))
          }}
        />
        <fieldset className="role-picker">
          <legend className="field-label">{m.role}</legend>
          {ROLES.map((item) => (
            <label key={item} className={['role-option', role === item && 'is-selected'].filter(Boolean).join(' ')}>
              <input type="radio" name="invite-role" value={item} checked={role === item} onChange={() => setRole(item)} />
              <strong>{ROLE_LABEL[item]}</strong>
              <span>{m.roleHint[item]}</span>
            </label>
          ))}
        </fieldset>
        {message ? <Alert tone="danger">{message}</Alert> : null}
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => (reset(), onClose())}>
            {c.cancel}
          </Button>
          <Button type="submit" loading={pending} icon={<MailPlus size={18} aria-hidden="true" />}>
            {m.send}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
