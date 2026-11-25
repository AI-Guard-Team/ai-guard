import { ArrowRight, ClipboardCheck, FolderOpen, Gavel, ListChecks, ScanSearch, ShieldAlert } from 'lucide-react'
import { Link } from 'react-router'
import { listExams } from '../../api/exams'
import { getReviewSessions } from '../../api/review'
import { Alert } from '../../components/Alert'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useResource } from '../../hooks/useResource'
import { greeting } from './greeting'
import { CardHead, FlowSteps, Hero, StatTile } from './OverviewParts'

const load = async (signal: AbortSignal) => {
  const exams = await listExams(signal)
  return Promise.all(exams.map(async (exam) => ({ exam, queue: await getReviewSessions(exam.id, {}, signal) })))
}

/** Değerlendirici genel bakışı: bekleyen dosyalar, sınav bazında kuyruklar ve değerlendirmenin adımları. */
export function ReviewerOverview({ firstName }: { firstName: string }) {
  const { data, error } = useResource(load, 'Bilgiler yüklenemedi.')
  const rows = data ?? []
  const pending = rows.reduce((sum, item) => sum + item.queue.pendingCount, 0)
  const decided = rows.reduce((sum, item) => sum + item.queue.sessions.filter((row) => row.status === 'Decided').length, 0)
  const completed = rows.reduce((sum, item) => sum + item.queue.sessions.filter((row) => row.status !== 'NotStarted').length, 0)
  // Sıradaki dosya: en çok bekleyen sınavın ilk karar bekleyen dosyası (liste sunucuda öncelik sıralı).
  const busiest = [...rows].sort((a, b) => b.queue.pendingCount - a.queue.pendingCount)[0]
  const nextCase = busiest?.queue.sessions.find((row) => row.status === 'AwaitingReview')

  return (
    <div className="overview">
      <Hero
        greeting={greeting(firstName)}
        text={data ? (pending > 0 ? `${pending} dosya kararınızı bekliyor. Olayları inceleyin, yanlış alarmları ayıklayın, kararı siz verin.` : 'Karar bekleyen dosya yok. Yeni bir dosya düştüğünde inceleme panelinde görünür.') : 'Dosyalar yükleniyor…'}
        aside={
          nextCase ? (
            <Link className="button button-lg hero-button" to={`/review/sessions/${nextCase.sessionId}`}>
              İncelemeye başla
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          ) : (
            <Link className="button button-lg hero-button" to="/review">
              İncelemelere git
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          )
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={FolderOpen} tone="amber" value={data ? pending : '–'} label="Karar bekleyen" to="/review" />
        <StatTile icon={Gavel} tone="mint" value={data ? decided : '–'} label="Karara bağlanan" index={1} />
        <StatTile icon={ClipboardCheck} tone="sky" value={data ? completed : '–'} label="Tamamlanan oturum" index={2} />
        <StatTile icon={ListChecks} tone="lilac" value={data ? rows.length : '–'} label="Atandığınız sınav" to="/review" index={3} />
      </ul>

      <section className="overview-card">
        <CardHead title="Sınavlarınız" to="/review" />
        {!data ? (
          <Skeleton height="8rem" />
        ) : rows.length === 0 ? (
          <p className="overview-empty">Henüz bir sınava değerlendirici olarak atanmadınız.</p>
        ) : (
          <ul className="overview-list">
            {rows.map(({ exam, queue }) => (
              <li key={exam.id}>
                <Link to={`/review/exams/${exam.id}`}>
                  <span>
                    <strong>{exam.name}</strong>
                    <span className="overview-sub">{queue.sessions.length} dosya</span>
                  </span>
                  {queue.pendingCount > 0 ? <StatusBadge tone="warning" dot>{`${queue.pendingCount} karar bekliyor`}</StatusBadge> : <StatusBadge tone="success" dot>Bekleyen yok</StatusBadge>}
                  <ArrowRight size={18} aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <FlowSteps
        title="Değerlendirme nasıl yapılır?"
        steps={[
          { icon: FolderOpen, tone: 'amber', title: 'Dosyayı açın', text: 'Riskli ve işaretli oturumlar inceleme panelinde en üstte.' },
          { icon: ScanSearch, tone: 'sky', title: 'Olaylara bakın', text: 'Sinyal kaydında ne zaman ne olduğunu, kanıt karesini görün.' },
          { icon: ShieldAlert, tone: 'rose', title: 'Yanlış alarmı ayıklayın', text: 'Yanlış alarm olay silinmez, skordan çıkar.' },
          { icon: Gavel, tone: 'mint', title: 'Karar verin', text: 'Temiz, Şüpheli ya da İhlal; karar bir kez verilir.' },
        ]}
      />
    </div>
  )
}
