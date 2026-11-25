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
import { formatDateTime } from '../../lib/format'
import { windowPhase } from '../admin/examWindow'
import { ACTION_LABEL } from '../admin/auditLabels'
import { greeting } from './greeting'
import { CardHead, Hero, StatTile } from './OverviewParts'

const load = async (signal: AbortSignal) => {
  const [exams, users, audit] = await Promise.all([listExams(signal), listUsers(undefined, signal), listAuditLogs({ page: 1, pageSize: 6 }, signal)])
  const queues = await Promise.all(exams.map((exam) => getReviewSessions(exam.id, {}, signal)))
  return { exams, users, audit, pending: queues.reduce((sum, queue) => sum + queue.pendingCount, 0) }
}

/** Yönetici genel bakışı: sınavlar, kullanıcılar, bekleyen dosyalar, hızlı eylemler ve son işlemler. */
export function AdminOverview({ firstName }: { firstName: string }) {
  const { data, error } = useResource(load, 'Bilgiler yüklenemedi.')
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
        text="Sınavları kurun, kullanıcıları yönetin, raporları ve denetim kaydını izleyin. Kopya kararını sistem değil, atadığınız değerlendiriciler verir."
        aside={
          <Link className="button button-lg hero-button" to="/admin/exams/new">
            <Plus size={18} aria-hidden="true" />
            Yeni sınav
          </Link>
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={BookOpenCheck} tone="sky" value={data ? open : '–'} label="Erişime açık sınav" to="/admin/exams" />
        <StatTile icon={CalendarClock} tone="lilac" value={data ? upcoming : '–'} label="Planlanan sınav" to="/admin/exams" index={1} />
        <StatTile icon={Users} tone="indigo" value={data ? data.users.filter((user) => user.isActive).length : '–'} label="Aktif kullanıcı" hint={data ? `${roleCount('Candidate')} aday · ${roleCount('Reviewer')} değerlendirici` : undefined} to="/admin/users" index={2} />
        <StatTile icon={FolderOpen} tone="amber" value={data ? data.pending : '–'} label="Karar bekleyen dosya" to="/admin/reports" index={3} />
      </ul>

      <div className="overview-grid">
        <section className="overview-card">
          <CardHead title="Hızlı eylemler" />
          <ul className="quick-actions">
            <li>
              <Link to="/admin/exams/new">
                <IconTile icon={Plus} tone="sky" size="sm" />
                <span>
                  <strong>Sınav oluştur</strong>
                  <span className="overview-sub">Genel bilgiler, sorular, atamalar</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
            <li>
              <Link to="/admin/users">
                <IconTile icon={MailPlus} tone="lilac" size="sm" />
                <span>
                  <strong>Kullanıcı davet et</strong>
                  <span className="overview-sub">Aday, değerlendirici ya da yönetici</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
            <li>
              <Link to="/admin/reports">
                <IconTile icon={FileBarChart} tone="mint" size="sm" />
                <span>
                  <strong>Raporlara bak</strong>
                  <span className="overview-sub">Aday raporları, PDF ve Excel</span>
                </span>
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
            </li>
          </ul>
          {noQuestions.length ? (
            <Alert tone="warning" title="Sorusu olmayan sınav var">
              {noQuestions.map((exam) => (
                <Link key={exam.id} className="overview-inline-link" to={`/admin/exams/${exam.id}/questions`}>
                  {exam.name}
                </Link>
              ))}
            </Alert>
          ) : null}
        </section>

        <section className="overview-card">
          <CardHead title="Son işlemler" to="/admin/audit" linkLabel="Denetim kaydı" />
          {!data ? (
            <Skeleton height="10rem" />
          ) : data.audit.items.length === 0 ? (
            <p className="overview-empty">Henüz kayıt yok.</p>
          ) : (
            <ol className="activity">
              {data.audit.items.map((item) => (
                <li key={item.id}>
                  <IconTile icon={ScrollText} tone="slate" size="sm" />
                  <span>
                    <strong>{item.actorName ?? 'Sistem'}</strong> {(ACTION_LABEL[item.action] ?? item.action).toLocaleLowerCase('tr-TR')}
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
