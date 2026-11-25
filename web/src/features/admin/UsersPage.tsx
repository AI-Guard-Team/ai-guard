import { MailPlus, Search, UserRoundX, UserRoundCheck, Users } from 'lucide-react'
import { motion } from 'motion/react'
import { useMemo, useState, type FormEvent } from 'react'
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
import { formatDate } from '../../lib/format'
import './admin.css'

const ROLES: UserRole[] = ['Candidate', 'Reviewer', 'Admin']
const PLURAL: Record<UserRole, string> = { Candidate: 'Adaylar', Reviewer: 'Değerlendiriciler', Admin: 'Yöneticiler' }
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const INVITE: Record<InviteStatus, { label: string; tone: BadgeTone }> = {
  Accepted: { label: 'Hesap etkin', tone: 'success' },
  Pending: { label: 'Davet bekliyor', tone: 'info' },
  Expired: { label: 'Davetin süresi doldu', tone: 'warning' },
}

const load = (signal: AbortSignal) => listUsers(undefined, signal)

/** Kullanıcılar (Stitch 23) ve davet (Stitch 24). Hesabı yönetici açar; kullanıcıya şifre belirleme bağlantısı gider (K-20). */
export function UsersPage() {
  const { user: me } = useAuth()
  const { data, error, loading, reload } = useResource(load, 'Kullanıcılar yüklenemedi.')
  const showToast = useToast()
  const [role, setRole] = useState<UserRole | 'all'>('all')
  const [query, setQuery] = useState('')
  const [inviting, setInviting] = useState(false)
  const [toggling, setToggling] = useState<UserSummary | null>(null)
  const [busy, setBusy] = useState<string | null>(null)

  const rows = useMemo(() => {
    const term = query.trim().toLocaleLowerCase('tr-TR')
    return (data ?? [])
      .filter((user) => role === 'all' || user.role === role)
      .filter((user) => !term || user.fullName.toLocaleLowerCase('tr-TR').includes(term) || user.email.toLocaleLowerCase('tr-TR').includes(term))
      .sort((a, b) => a.fullName.localeCompare(b.fullName, 'tr'))
  }, [data, role, query])

  const counts = useMemo(() => {
    const result: Record<UserRole | 'all', number> = { all: data?.length ?? 0, Candidate: 0, Reviewer: 0, Admin: 0 }
    for (const user of data ?? []) result[user.role] += 1
    return result
  }, [data])

  const changeRole = async (user: UserSummary, next: UserRole) => {
    setBusy(user.id)
    try {
      await updateUser(user.id, { role: next })
      showToast(`${user.fullName} artık ${ROLE_LABEL[next].toLocaleLowerCase('tr-TR')}.`, 'success')
      reload()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Rol değiştirilemedi.', 'error')
    } finally {
      setBusy(null)
    }
  }

  const toggleActive = async () => {
    if (!toggling) return
    setBusy(toggling.id)
    try {
      await updateUser(toggling.id, { isActive: !toggling.isActive })
      showToast(toggling.isActive ? 'Hesap pasife alındı.' : 'Hesap yeniden etkinleştirildi.', 'success')
      setToggling(null)
      reload()
    } catch (caught) {
      showToast(caught instanceof ApiError ? caught.message : 'Hesap güncellenemedi.', 'error')
      setToggling(null)
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }]}
        title="Kullanıcılar"
        description="Hesap açın, rolleri yönetin, gerekirse hesabı pasife alın."
        info={
          <ul>
            <li>Yeni kullanıcıya şifre belirleme bağlantısı içeren bir davet e-postası gider.</li>
            <li>Pasife alınan kullanıcı giriş yapamaz; kayıtları silinmez.</li>
            <li>Rol değişikliği ve pasife alma denetim kaydına yazılır.</li>
          </ul>
        }
        actions={
          <Button icon={<MailPlus size={18} aria-hidden="true" />} onClick={() => setInviting(true)}>
            Kullanıcı davet et
          </Button>
        }
      />

      <div className="admin-toolbar">
        <div className="chip-tabs" role="group" aria-label="Role göre süz">
          {(['all', ...ROLES] as const).map((key) => (
            <button key={key} type="button" className="chip-tab" aria-pressed={role === key} onClick={() => setRole(key)}>
              {key === 'all' ? 'Tümü' : PLURAL[key]} <span className="chip-count">{counts[key]}</span>
            </button>
          ))}
        </div>
        <label className="search-field">
          <Search size={18} aria-hidden="true" />
          <span className="visually-hidden">Kullanıcı ara</span>
          <input type="search" placeholder="Ad ya da e-postayla ara" value={query} onChange={(event) => setQuery(event.target.value)} />
        </label>
      </div>

      {error ? (
        <Alert tone="danger" title="Kullanıcılar yüklenemedi">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      {loading && !data ? (
        <Skeleton height="20rem" radius="lg" />
      ) : rows.length === 0 ? (
        <EmptyState icon={Users} title={data?.length ? 'Eşleşen kullanıcı yok' : 'Henüz kullanıcı yok'}>
          {data?.length ? 'Aramayı ya da filtreyi değiştirin.' : '"Kullanıcı davet et" ile ilk hesabı açın.'}
        </EmptyState>
      ) : (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Kullanıcı</th>
                <th scope="col">Rol</th>
                <th scope="col">Durum</th>
                <th scope="col">Eklenme</th>
                <th scope="col">
                  <span className="visually-hidden">İşlem</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((user, index) => {
                const self = user.id === me?.id
                return (
                  <motion.tr key={user.id} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: Math.min(index, 10) * 0.02 }} className={user.isActive ? undefined : 'is-muted'}>
                    <td>
                      <span className="person">
                        <span className="assign-avatar" aria-hidden="true">
                          {user.fullName.slice(0, 1).toLocaleUpperCase('tr-TR')}
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
                        aria-label={`${user.fullName} rolü`}
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
                        <StatusBadge tone={INVITE[user.inviteStatus].tone} dot>
                          {INVITE[user.inviteStatus].label}
                        </StatusBadge>
                      ) : (
                        <StatusBadge tone="neutral" dot>
                          Pasif
                        </StatusBadge>
                      )}
                    </td>
                    <td className="numeric">{formatDate(user.createdAt)}</td>
                    <td className="cell-actions">
                      {self ? (
                        <span className="admin-row-sub">Siz</span>
                      ) : (
                        <Button
                          variant="ghost"
                          icon={user.isActive ? <UserRoundX size={16} aria-hidden="true" /> : <UserRoundCheck size={16} aria-hidden="true" />}
                          onClick={() => setToggling(user)}
                        >
                          {user.isActive ? 'Pasife al' : 'Etkinleştir'}
                        </Button>
                      )}
                    </td>
                  </motion.tr>
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
          showToast(`${name} için davet e-postası gönderildi.`, 'success')
          reload()
        }}
      />

      <Modal open={toggling !== null} title={toggling?.isActive ? 'Hesap pasife alınsın mı?' : 'Hesap etkinleştirilsin mi?'} onClose={() => setToggling(null)}>
        <p>
          {toggling?.isActive
            ? `${toggling?.fullName} giriş yapamayacak; açık oturumu en geç 15 dakika içinde kapanır. Kayıtları silinmez, istediğinizde yeniden etkinleştirebilirsiniz.`
            : `${toggling?.fullName} yeniden giriş yapabilecek.`}
        </p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setToggling(null)}>
            Vazgeç
          </Button>
          <Button variant={toggling?.isActive ? 'danger' : 'primary'} loading={busy === toggling?.id} onClick={() => void toggleActive()}>
            {toggling?.isActive ? 'Pasife al' : 'Etkinleştir'}
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

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const next = {
      fullName: fullName.trim() ? undefined : 'Ad soyad zorunludur.',
      email: !email.trim() ? 'E-posta zorunludur.' : EMAIL_PATTERN.test(email.trim()) ? undefined : 'Geçerli bir e-posta adresi girin.',
    }
    setErrors(next)
    if (next.fullName || next.email) return
    setPending(true)
    setMessage(null)
    try {
      await createUser({ fullName: fullName.trim(), email: email.trim(), role })
      onInvited(fullName.trim())
      reset()
    } catch (caught) {
      setMessage(caught instanceof ApiError ? caught.message : 'Kullanıcı oluşturulamadı.')
    } finally {
      setPending(false)
    }
  }

  return (
    <Modal open={open} title="Kullanıcı davet et" onClose={() => (reset(), onClose())}>
      <form onSubmit={submit} noValidate>
        <p className="form-note">Kullanıcıya şifresini belirleyeceği bir bağlantı e-postayla gider.</p>
        <Input label="Ad soyad" value={fullName} error={errors.fullName} maxLength={200} onChange={(event) => setFullName(event.target.value)} />
        <Input label="E-posta" type="email" autoComplete="off" value={email} error={errors.email} onChange={(event) => setEmail(event.target.value)} />
        <fieldset className="role-picker">
          <legend className="field-label">Rol</legend>
          {ROLES.map((item) => (
            <label key={item} className={['role-option', role === item && 'is-selected'].filter(Boolean).join(' ')}>
              <input type="radio" name="invite-role" value={item} checked={role === item} onChange={() => setRole(item)} />
              <strong>{ROLE_LABEL[item]}</strong>
              <span>{item === 'Candidate' ? 'Atandığı sınavlara girer' : item === 'Reviewer' ? 'Dosyaları inceler, karar verir' : 'Sınav, kullanıcı ve ayarları yönetir'}</span>
            </label>
          ))}
        </fieldset>
        {message ? <Alert tone="danger">{message}</Alert> : null}
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => (reset(), onClose())}>
            Vazgeç
          </Button>
          <Button type="submit" loading={pending} icon={<MailPlus size={18} aria-hidden="true" />}>
            Davet gönder
          </Button>
        </div>
      </form>
    </Modal>
  )
}
