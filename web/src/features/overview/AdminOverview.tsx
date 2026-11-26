import { ArrowRight, BookOpenCheck, CalendarClock, FileBarChart, FolderOpen, MailPlus, Plus, ScrollText, Users } from 'lucide-react'
import { Link } from 'react-router'
import { listAuditLogs, listUsers } from '../../api/admin'
import { listExams } from '../../api/exams'
import { getReviewSessions } from '../../api/review'
import { Alert } from '../../components/Alert'
import { IconTile } from '../../components/IconTile'
import { Skeleton } from '../../components/Skeleton'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { getLocale } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import { windowPhase } from '../admin/examWindow'
import { ACTION_LABEL } from '../admin/auditLabels'
import { greeting } from './greeting'
import { CardHead, Hero, StatTile } from './OverviewParts'

const messages = defineMessages({
  tr: {
    failed: 'Bilgiler yüklenemedi.',
    hero: 'Sınavlar, kullanıcılar ve bekleyen işler bir bakışta.',
    newExam: 'Yeni sınav',
    open: 'Erişime açık sınav',
    upcoming: 'Planlanan sınav',
    active: 'Aktif kullanıcı',
    roles: (candidates: number, reviewers: number) => `${candidates} aday · ${reviewers} değerlendirici`,
    pending: 'Karar bekleyen dosya',
    quick: 'Hızlı eylemler',
    createExam: 'Sınav oluştur',
    createExamText: 'Genel bilgiler, sorular, atamalar',
    invite: 'Kullanıcı davet et',
    inviteText: 'Aday, değerlendirici ya da yönetici',
    reports: 'Raporlara bak',
    reportsText: 'Aday raporları, PDF ve Excel',
    noQuestions: 'Sorusu olmayan sınav var',
    recent: 'Son işlemler',
    audit: 'Denetim kaydı',
    noRecords: 'Henüz kayıt yok.',
    system: 'Sistem',
  },
  en: {
    failed: 'The information could not be loaded.',
    hero: 'Exams, users and pending work at a glance.',
    newExam: 'New exam',
    open: 'Open exams',
    upcoming: 'Scheduled exams',
    active: 'Active users',
    roles: (candidates: number, reviewers: number) => `${candidates} candidates · ${reviewers} reviewers`,
    pending: 'Files awaiting decision',
    quick: 'Quick actions',
    createExam: 'Create an exam',
    createExamText: 'General details, questions, assignments',
    invite: 'Invite a user',
    inviteText: 'Candidate, reviewer or administrator',
    reports: 'View reports',
    reportsText: 'Candidate reports, PDF and Excel',
    noQuestions: 'Some exams have no questions',
    recent: 'Recent activity',
    audit: 'Audit log',
    noRecords: 'No records yet.',
    system: 'System',
  },
})

const load = async (signal: AbortSignal) => {
  const [exams, users, audit] = await Promise.all([listExams(signal), listUsers(undefined, signal), listAuditLogs({ page: 1, pageSize: 6 }, signal)])
  const queues = await Promise.all(exams.map((exam) => getReviewSessions(exam.id, {}, signal)))
  return { exams, users, audit, pending: queues.reduce((sum, queue) => sum + queue.pendingCount, 0) }
}

/** Yönetici genel bakışı: sınavlar, kullanıcılar, bekleyen dosyalar, hızlı eylemler ve son işlemler. */
export function AdminOverview({ firstName }: { firstName: string }) {
  const m = useMessages(messages)
  const { data, error } = useResource(load, m.failed)
  const now = useNow(60_000).getTime()

  const phases = data?.exams.map((exam) => windowPhase(exam, now)) ?? []
  const open = phases.filter((phase) => phase === 'open' || phase === 'closing').length
  const upcoming = phases.filter((phase) => phase === 'upcoming').length
  const noQuestions = data?.exams.filter((exam) => exam.questionCount === 0) ?? []
  const roleCount = (role: string) => data?.users.filter((user) => user.role === role && user.isActive).length ?? 0

  return (
    <div className="overview">
      <Hero
        greeting={greeting(firstName)}
        text={m.hero}
        aside={
          <Link className="button button-lg hero-button" to="/admin/exams/new">
            <Plus size={18} aria-hidden="true" />
            {m.newExam}
          </Link>
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={BookOpenCheck} tone="sky" value={data ? open : '–'} label={m.open} to="/admin/exams" />
        <StatTile icon={CalendarClock} tone="lilac" value={data ? upcoming : '–'} label={m.upcoming} to="/admin/exams" index={1} />
        <StatTile
          icon={Users}
          tone="indigo"
          value={data ? data.users.filter((user) => user.isActive).length : '–'}
          label={m.active}
          hint={data ? m.roles(roleCount('Candidate'), roleCount('Reviewer')) : undefined}
          to="/admin/users"
          index={2}
        />
        <StatTile icon={FolderOpen} tone="amber" value={data ? data.pending : '–'} label={m.pending} to="/admin/reports" index={3} />
      </ul>

      <div className="overview-grid">
        <section className="overview-card">
          <CardHead title={m.quick} />
          <ul className="quick-actions">
            <li>
              <Link to="/admin/exams/new">
                <IconTile icon={Plus} tone="sky" size="sm" />
                <span>
                  <strong>{m.createExam}</strong>
                  <span className="overview-sub">{m.createExamText}</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
            <li>
              <Link to="/admin/users">
                <IconTile icon={MailPlus} tone="lilac" size="sm" />
                <span>
                  <strong>{m.invite}</strong>
                  <span className="overview-sub">{m.inviteText}</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
            <li>
              <Link to="/admin/reports">
                <IconTile icon={FileBarChart} tone="mint" size="sm" />
                <span>
                  <strong>{m.reports}</strong>
                  <span className="overview-sub">{m.reportsText}</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
          </ul>
          {noQuestions.length ? (
            <Alert tone="warning" title={m.noQuestions}>
              {noQuestions.map((exam) => (
                <Link key={exam.id} className="overview-inline-link" to={`/admin/exams/${exam.id}/questions`}>
                  {exam.name}
                </Link>
              ))}
            </Alert>
          ) : null}
        </section>

        <section className="overview-card">
          <CardHead title={m.recent} to="/admin/audit" linkLabel={m.audit} />
          {!data ? (
            <Skeleton height="10rem" />
          ) : data.audit.items.length === 0 ? (
            <p className="overview-empty">{m.noRecords}</p>
          ) : (
            <ol className="activity">
              {data.audit.items.map((item) => (
                <li key={item.id}>
                  <IconTile icon={ScrollText} tone="slate" size="sm" />
                  <span>
                    <strong>{item.actorName ?? m.system}</strong> {(ACTION_LABEL[item.action] ?? item.action).toLocaleLowerCase(getLocale())}
                    <span className="overview-sub">{formatDateTime(item.occurredAt)}</span>
                  </span>
                </li>
              ))}
            </ol>
          )}
        </section>
      </div>
    </div>
  )
}
