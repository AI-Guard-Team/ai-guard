import { BookOpenCheck, Check, ListChecks, ShieldAlert, Users } from 'lucide-react'
import { useCallback } from 'react'
import { NavLink, Navigate, useParams } from 'react-router'
import { getExam } from '../../api/exams'
import { listAssignments } from '../../api/admin'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { StatusBadge } from '../../components/StatusBadge'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { AssignmentsTab } from './AssignmentsTab'
import { PHASE_LABEL, PHASE_TONE, windowPhase } from './examWindow'
import { ExamGeneralForm } from './ExamGeneralForm'
import { QuestionsTab } from './QuestionsTab'
import { RiskTab } from './RiskTab'
import './admin.css'

type Step = 'general' | 'questions' | 'assignments' | 'risk'

const STEPS: { key: Step; icon: typeof BookOpenCheck }[] = [
  { key: 'general', icon: BookOpenCheck },
  { key: 'questions', icon: ListChecks },
  { key: 'assignments', icon: Users },
  { key: 'risk', icon: ShieldAlert },
]

const messages = defineMessages({
  tr: {
    steps: {
      general: { label: 'Genel bilgiler', hint: 'Ad, tür, süre ve erişim penceresi' },
      questions: { label: 'Sorular', hint: 'Çoktan seçmeli sorular' },
      assignments: { label: 'Atamalar', hint: 'Adaylar ve değerlendiriciler' },
      risk: { label: 'Risk ayarları', hint: 'Olay ağırlıkları ve eşik' },
    } as Record<Step, { label: string; hint: string }>,
    failed: 'Sınav yüklenemedi.',
    saved: 'Kaydedildi',
    start: 'Başlangıç',
    generalFirst: 'Önce genel bilgiler',
    assigned: (candidates: number, reviewers: number) => `${candidates} aday · ${reviewers} değerlendirici`,
    platformWide: 'Platform geneli',
    admin: 'Yönetim',
    exams: 'Sınavlar',
    newExam: 'Yeni sınav',
    exam: 'Sınav',
    newDescription: 'Önce genel bilgileri kaydedin; ardından soruları ve atamaları ekleyin.',
    openFailed: 'Sınav açılamadı',
    stepsLabel: 'Sınav adımları',
  },
  en: {
    steps: {
      general: { label: 'General details', hint: 'Name, type, duration and access window' },
      questions: { label: 'Questions', hint: 'Multiple-choice questions' },
      assignments: { label: 'Assignments', hint: 'Candidates and reviewers' },
      risk: { label: 'Risk settings', hint: 'Event weights and threshold' },
    },
    failed: 'The exam could not be loaded.',
    saved: 'Saved',
    start: 'Start here',
    generalFirst: 'General details first',
    assigned: (candidates: number, reviewers: number) => `${candidates} candidates · ${reviewers} reviewers`,
    platformWide: 'Platform-wide',
    admin: 'Administration',
    exams: 'Exams',
    newExam: 'New exam',
    exam: 'Exam',
    newDescription: 'Save the general details first, then add questions and assignments.',
    openFailed: 'The exam could not be opened',
    stepsLabel: 'Exam steps',
  },
})

/**
 * Sınav oluşturma ve düzenleme (Stitch 26–29, Prompt G): solda dört adımlı içindekiler, sağda seçili adım.
 * Yeni sınavda yalnızca "Genel bilgiler" açık; kaydedilince sınavın kendi adresine geçilir ve diğer adımlar açılır.
 */
export function ExamEditorPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  const { examId, step = 'general' } = useParams()
  const isNew = !examId
  const now = useNow(60_000)

  const load = useCallback(
    async (signal: AbortSignal) => {
      if (!examId) return null
      const [exam, assignments] = await Promise.all([getExam(examId, signal), listAssignments(examId, signal)])
      return { exam: exam ?? null, assignments }
    },
    [examId],
  )
  const { data, error, loading, reload } = useResource(load, m.failed)

  if (!STEPS.some((item) => item.key === step)) return <Navigate to={examId ? `/admin/exams/${examId}` : '/admin/exams/new'} replace />
  if (isNew && step !== 'general') return <Navigate to="/admin/exams/new" replace />

  const exam = data?.exam ?? null
  const candidates = data?.assignments.filter((item) => item.role === 'Candidate').length ?? 0
  const reviewers = data?.assignments.filter((item) => item.role === 'Reviewer').length ?? 0
  const done: Record<Step, boolean> = {
    general: Boolean(exam),
    questions: (exam?.questionCount ?? 0) > 0,
    assignments: candidates > 0 && reviewers > 0,
    risk: false,
  }
  const progress: Record<Step, string> = {
    general: exam ? m.saved : m.start,
    questions: exam ? c.questions(exam.questionCount) : m.generalFirst,
    assignments: exam ? m.assigned(candidates, reviewers) : m.generalFirst,
    risk: m.platformWide,
  }
  const phase = exam ? windowPhase(exam, now.getTime()) : null
  const current = STEPS.find((item) => item.key === step)!

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: m.admin }, { label: m.exams, to: '/admin/exams' }]}
        title={isNew ? m.newExam : (exam?.name ?? m.exam)}
        description={isNew ? m.newDescription : m.steps[current.key].hint}
        actions={phase ? <StatusBadge tone={PHASE_TONE[phase]} dot>{PHASE_LABEL[phase]}</StatusBadge> : null}
      />

      {error && !data ? (
        <Alert tone="danger" title={m.openFailed}>
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : null}

      <div className="editor">
        <nav className="editor-steps" aria-label={m.stepsLabel}>
          <ol>
            {STEPS.map((item, index) => {
              const disabled = isNew && item.key !== 'general'
              const Icon = item.icon
              const content = (
                <>
                  <span className={['editor-step-mark', done[item.key] && 'is-done'].filter(Boolean).join(' ')} aria-hidden="true">
                    {done[item.key] ? <Check size={16} strokeWidth={3} /> : <Icon size={16} />}
                  </span>
                  <span className="editor-step-text">
                    <span className="editor-step-label">
                      {index + 1}. {m.steps[item.key].label}
                    </span>
                    <span className="editor-step-hint">{progress[item.key]}</span>
                  </span>
                </>
              )
              return (
                <li key={item.key}>
                  {disabled ? (
                    <span className="editor-step is-disabled" aria-disabled="true">
                      {content}
                    </span>
                  ) : (
                    <NavLink className="editor-step" to={item.key === 'general' ? `/admin/exams/${examId}` : `/admin/exams/${examId}/${item.key}`} end>
                      {content}
                    </NavLink>
                  )}
                </li>
              )
            })}
          </ol>
        </nav>

        <section className="editor-body" aria-label={m.steps[current.key].label}>
          {!isNew && loading && !data ? (
            <Skeleton height="24rem" radius="lg" />
          ) : step === 'general' ? (
            <ExamGeneralForm exam={exam} onSaved={reload} />
          ) : !exam ? null : step === 'questions' ? (
            <QuestionsTab examId={exam.id} onChanged={reload} />
          ) : step === 'assignments' ? (
            <AssignmentsTab examId={exam.id} assignments={data?.assignments ?? []} onChanged={reload} />
          ) : (
            <RiskTab />
          )}
        </section>
      </div>
    </div>
  )
}
