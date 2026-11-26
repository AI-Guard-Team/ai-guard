import { ArrowRight, Camera, CircleCheck, CircleDashed, ClipboardCheck, ClipboardList, Clock3 } from 'lucide-react'
import { Link } from 'react-router'
import { EXAM_TYPE_LABEL, listExams, type ExamSummary } from '../../api/exams'
import { getBiometricStatus } from '../../api/profile'
import { Alert } from '../../components/Alert'
import { Skeleton } from '../../components/Skeleton'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, pick, useMessages } from '../../i18n/messages'
import { formatDateTime } from '../../lib/format'
import { greeting } from './greeting'
import { CardHead, Hero, StatTile } from './OverviewParts'

const messages = defineMessages({
  tr: {
    failed: 'Bilgiler yüklenemedi.',
    hero: 'Sınavlarınız ve hazırlık durumunuz bir bakışta.',
    toExams: 'Sınavlarıma git',
    assigned: 'Atanan sınav',
    completed: 'Tamamlanan',
    ready: 'Hazır',
    missing: 'Eksik',
    photo: 'Kayıt fotoğrafı',
    next: 'Sıradaki sınavınız',
    running: 'Sınavınız sürüyor',
    opensIn: (span: string, at: string) => `${span} sonra açılıyor · ${at}`,
    open: (at: string) => `Erişime açık · son başlangıç ${at}`,
    noNext: 'Şu an bekleyen sınavınız yok. Yeni bir sınava atandığınızda burada görünür.',
    checklist: 'Hazırlık listesi',
    photoDone: 'Yüklendi; kimliğiniz sınav başında bununla doğrulanır.',
    photoTodo: 'Sınava başlamadan önce bir kez yükleyin.',
    upload: 'Yükle',
    media: 'Kamera ve mikrofon',
    mediaText: 'Sınav boyunca açık kalır; tarayıcı izin isterse "İzin ver"i seçin.',
    agent: 'Güvenlik ajanı',
    agentText: 'Sınav öncesi adımlarda kurulumu ve eşleştirmesi anlatılır.',
    days: (d: number, h: number) => `${d} gün ${h} saat`,
    hours: (h: number, min: number) => `${h} saat ${min} dk`,
    mins: (min: number) => `${min} dk`,
  },
  en: {
    failed: 'The information could not be loaded.',
    hero: 'Your exams and preparation at a glance.',
    toExams: 'Go to my exams',
    assigned: 'Assigned exams',
    completed: 'Completed',
    ready: 'Ready',
    missing: 'Missing',
    photo: 'Reference photo',
    next: 'Your next exam',
    running: 'Your exam is in progress',
    opensIn: (span: string, at: string) => `Opens in ${span} · ${at}`,
    open: (at: string) => `Open now · latest start ${at}`,
    noNext: 'You have no pending exams. When you are assigned a new exam, it appears here.',
    checklist: 'Preparation checklist',
    photoDone: 'Uploaded; your identity is checked against it at the start of the exam.',
    photoTodo: 'Upload it once before your first exam.',
    upload: 'Upload',
    media: 'Camera and microphone',
    mediaText: 'They stay on during the exam; if the browser asks, choose "Allow".',
    agent: 'Security agent',
    agentText: 'Setup and pairing are explained in the pre-exam steps.',
    days: (d: number, h: number) => `${d} d ${h} h`,
    hours: (h: number, min: number) => `${h} h ${min} min`,
    mins: (min: number) => `${min} min`,
  },
})

const load = async (signal: AbortSignal) => {
  const [exams, biometric] = await Promise.all([listExams(signal), getBiometricStatus(signal)])
  return { exams, biometric }
}

const FINISHED = new Set(['Completed', 'Decided'])
const RUNNING = new Set(['InProgress', 'PermissionPending', 'Disconnected'])

/** Kalan süre "3 gün 4 saat" / "2 saat 10 dk" / "12 dk" biçiminde. */
function until(target: number, now: number): string {
  const m = pick(messages)
  const minutes = Math.max(0, Math.round((target - now) / 60_000))
  const days = Math.floor(minutes / 1440)
  const hours = Math.floor((minutes % 1440) / 60)
  if (days > 0) return m.days(days, hours)
  if (hours > 0) return m.hours(hours, minutes % 60)
  return m.mins(minutes)
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
  const m = useMessages(messages)
  const c = useMessages(common)
  const { data, error } = useResource(load, m.failed)
  const now = useNow(30_000).getTime()

  const exams = data?.exams ?? []
  const done = exams.filter((exam) => FINISHED.has(exam.mySession?.state ?? '')).length
  const next = data ? nextExam(exams, now) : undefined
  const enrolled = data?.biometric.enrolled ?? false

  return (
    <div className="overview">
      <Hero
        greeting={greeting(firstName)}
        text={m.hero}
        aside={
          <Link className="button button-lg hero-button" to="/exams">
            {m.toExams}
            <ArrowRight size={18} aria-hidden="true" />
          </Link>
        }
      />

      {error && !data ? <Alert tone="danger">{error}</Alert> : null}

      <ul className="stat-grid">
        <StatTile icon={ClipboardList} tone="sky" value={data ? exams.length : '–'} label={m.assigned} to="/exams" />
        <StatTile icon={ClipboardCheck} tone="mint" value={data ? done : '–'} label={m.completed} index={1} />
        <StatTile icon={Camera} tone="rose" value={data ? (enrolled ? m.ready : m.missing) : '–'} label={m.photo} to="/profile/photo" index={2} />
      </ul>

      <div className="overview-grid">
        <section className="overview-card next-exam" aria-label={m.next}>
          <CardHead title={m.next} to="/exams" />
          {!data ? (
            <Skeleton height="8rem" />
          ) : next ? (
            <div className="next-exam-body">
              <p className="next-exam-type">{EXAM_TYPE_LABEL[next.type]}</p>
              <h3>{next.name}</h3>
              <p className="next-exam-when">
                <Clock3 size={16} aria-hidden="true" />
                {RUNNING.has(next.mySession?.state ?? '')
                  ? m.running
                  : now < new Date(next.windowStartAt).getTime()
                    ? m.opensIn(until(new Date(next.windowStartAt).getTime(), now), formatDateTime(next.windowStartAt))
                    : m.open(formatDateTime(next.latestStartAt))}
              </p>
              <p className="next-exam-meta">
                {c.minutes(next.durationMinutes)} · {c.questions(next.questionCount)}
              </p>
            </div>
          ) : (
            <p className="overview-empty">{m.noNext}</p>
          )}
        </section>

        <section className="overview-card" aria-label={m.checklist}>
          <CardHead title={m.checklist} />
          <ul className="checklist">
            <li className={enrolled ? 'is-done' : undefined}>
              {enrolled ? <CircleCheck size={20} aria-hidden="true" /> : <CircleDashed size={20} aria-hidden="true" />}
              <span>
                <strong>{m.photo}</strong>
                <span>{enrolled ? m.photoDone : m.photoTodo}</span>
              </span>
              {!enrolled ? (
                <Link className="overview-link" to="/profile/photo">
                  {m.upload}
                  <ArrowRight size={16} aria-hidden="true" />
                </Link>
              ) : null}
            </li>
            <li>
              <CircleDashed size={20} aria-hidden="true" />
              <span>
                <strong>{m.media}</strong>
                <span>{m.mediaText}</span>
              </span>
            </li>
            <li>
              <CircleDashed size={20} aria-hidden="true" />
              <span>
                <strong>{m.agent}</strong>
                <span>{m.agentText}</span>
              </span>
            </li>
          </ul>
        </section>
      </div>
    </div>
  )
}
