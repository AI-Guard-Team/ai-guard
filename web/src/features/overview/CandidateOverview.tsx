import { ArrowRight, Camera, CircleCheck, CircleDashed, ClipboardCheck, ClipboardList, Clock3, FileCheck2, PlayCircle, ScanFace, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router'
import { EXAM_TYPE_LABEL, listExams, type ExamSummary } from '../../api/exams'
import { getBiometricStatus } from '../../api/profile'
import { Alert } from '../../components/Alert'
import { Skeleton } from '../../components/Skeleton'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { formatDateTime } from '../../lib/format'
import { greeting } from './greeting'
import { CardHead, FlowSteps, Hero, StatTile } from './OverviewParts'

const load = async (signal: AbortSignal) => {
  const [exams, biometric] = await Promise.all([listExams(signal), getBiometricStatus(signal)])
  return { exams, biometric }
}

const FINISHED = new Set(['Completed', 'Decided'])
const RUNNING = new Set(['InProgress', 'PermissionPending', 'Disconnected'])

/** Kalan süre "3 gün 4 saat" / "2 saat 10 dk" / "12 dk" biçiminde. */
function until(target: number, now: number): string {
  const minutes = Math.max(0, Math.round((target - now) / 60_000))
  const days = Math.floor(minutes / 1440)
  const hours = Math.floor((minutes % 1440) / 60)
  if (days > 0) return `${days} gün ${hours} saat`
  if (hours > 0) return `${hours} saat ${minutes % 60} dk`
  return `${minutes} dk`
}

/** Sıradaki sınav: sürmekte olan, sonra şu an başlatılabilen, sonra en yakın açılacak; bitmiş ve hakkı kullanılmışlar hariç. */
function nextExam(exams: ExamSummary[], now: number): ExamSummary | undefined {
  const pending = exams.filter((exam) => !FINISHED.has(exam.mySession?.state ?? '') && !exam.attemptUsed && new Date(exam.windowEndAt).getTime() > now)
  const running = pending.find((exam) => RUNNING.has(exam.mySession?.state ?? ''))
  if (running) return running
  return pending.sort((a, b) => new Date(a.windowStartAt).getTime() - new Date(b.windowStartAt).getTime())[0]
}

/** Aday genel bakışı: karşılama, sıradaki sınav, hazırlık listesi, sayılar ve sınavın nasıl ilerlediği. */
export function CandidateOverview({ firstName }: { firstName: string }) {
  const { data, error } = useResource(load, 'Bilgiler yüklenemedi.')
  const now = useNow(30_000).getTime()

  const exams = data?.exams ?? []
  const done = exams.filter((exam) => FINISHED.has(exam.mySession?.state ?? '')).length
  const next = data ? nextExam(exams, now) : undefined
  const enrolled = data?.biometric.enrolled ?? false

  return (
    <div className="overview">
      <Hero
        greeting={greeting(firstName)}
        text="Çevrim içi sınavlarınız burada. Sınav öncesi adımları tamamlayın, sınavınızı rahatça çözün; kararı bir değerlendirici verir."
        aside={
          <Link className="button button-lg hero-button" to="/exams">
            Sınavlarıma git
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={ClipboardList} tone="sky" value={data ? exams.length : '–'} label="Atanan sınav" to="/exams" />
        <StatTile icon={ClipboardCheck} tone="mint" value={data ? done : '–'} label="Tamamlanan" index={1} />
        <StatTile icon={Camera} tone="rose" value={data ? (enrolled ? 'Hazır' : 'Eksik') : '–'} label="Kayıt fotoğrafı" to="/profile/photo" index={2} />
      </ul>

      <div className="overview-grid">
        <section className="overview-card next-exam" aria-label="Sıradaki sınavınız">
          <CardHead title="Sıradaki sınavınız" to="/exams" />
          {!data ? (
            <Skeleton height="8rem" />
          ) : next ? (
            <div className="next-exam-body">
              <p className="next-exam-type">{EXAM_TYPE_LABEL[next.type]}</p>
              <h3>{next.name}</h3>
              <p className="next-exam-when">
                <Clock3 size={16} aria-hidden="true" />
                {RUNNING.has(next.mySession?.state ?? '')
                  ? 'Sınavınız sürüyor'
                  : now < new Date(next.windowStartAt).getTime()
                    ? `${until(new Date(next.windowStartAt).getTime(), now)} sonra açılıyor · ${formatDateTime(next.windowStartAt)}`
                    : `Erişime açık · son başlangıç ${formatDateTime(next.latestStartAt)}`}
              </p>
              <p className="next-exam-meta">
                {next.durationMinutes} dakika · {next.questionCount} soru
              </p>
            </div>
          ) : (
            <p className="overview-empty">Şu an bekleyen sınavınız yok. Yeni bir sınava atandığınızda burada görünür.</p>
          )}
        </section>

        <section className="overview-card" aria-label="Hazırlık listesi">
          <CardHead title="Hazırlık listesi" />
          <ul className="checklist">
            <li className={enrolled ? 'is-done' : undefined}>
              {enrolled ? <CircleCheck size={20} aria-hidden="true" /> : <CircleDashed size={20} aria-hidden="true" />}
              <span>
                <strong>Kayıt fotoğrafı</strong>
                <span>{enrolled ? 'Yüklendi; kimliğiniz sınav başında bununla doğrulanır.' : 'Sınava başlamadan önce bir kez yükleyin.'}</span>
              </span>
              {!enrolled ? (
                <Link className="overview-link" to="/profile/photo">
                  Yükle
                  <ArrowRight size={16} aria-hidden="true" />
                </Link>
              ) : null}
            </li>
            <li>
              <CircleDashed size={20} aria-hidden="true" />
              <span>
                <strong>Kamera ve mikrofon</strong>
                <span>Sınav boyunca açık kalır; tarayıcı izin isterse "İzin ver"i seçin.</span>
              </span>
            </li>
            <li>
              <CircleDashed size={20} aria-hidden="true" />
              <span>
                <strong>Güvenlik ajanı</strong>
                <span>Sınav öncesi adımlarda kurulumu ve eşleştirmesi anlatılır.</span>
              </span>
            </li>
          </ul>
        </section>
      </div>

      <FlowSteps
        title="Sınav nasıl ilerler?"
        steps={[
          { icon: FileCheck2, tone: 'lilac', title: 'Rıza', text: 'Hangi verinin neden işlendiğini okuyup onaylarsınız.' },
          { icon: ShieldCheck, tone: 'indigo', title: 'Güvenlik ajanı', text: 'Cihazınızdaki ajan eşleştirilir, ortam denetlenir.' },
          { icon: ScanFace, tone: 'rose', title: 'Kimlik doğrulama', text: 'Kayıt fotoğrafınızla karşılaştırılır; süre ancak sonra başlar.' },
          { icon: PlayCircle, tone: 'mint', title: 'Sınav', text: 'Cevaplar her seçimde kaydedilir; bağlantı koparsa cihazda bekler.' },
        ]}
      />
    </div>
  )
}
