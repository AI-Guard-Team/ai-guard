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
import { AssignmentsTab } from './AssignmentsTab'
import { PHASE_LABEL, PHASE_TONE, windowPhase } from './examWindow'
import { ExamGeneralForm } from './ExamGeneralForm'
import { QuestionsTab } from './QuestionsTab'
import { RiskTab } from './RiskTab'
import './admin.css'

type Step = 'general' | 'questions' | 'assignments' | 'risk'

const STEPS: { key: Step; label: string; hint: string; icon: typeof BookOpenCheck }[] = [
  { key: 'general', label: 'Genel bilgiler', hint: 'Ad, tür, süre ve erişim penceresi', icon: BookOpenCheck },
  { key: 'questions', label: 'Sorular', hint: 'Çoktan seçmeli sorular', icon: ListChecks },
  { key: 'assignments', label: 'Atamalar', hint: 'Adaylar ve değerlendiriciler', icon: Users },
  { key: 'risk', label: 'Risk ayarları', hint: 'Olay ağırlıkları ve eşik', icon: ShieldAlert },
]

/**
 * Sınav oluşturma ve düzenleme (Stitch 26–29, Prompt G): solda dört adımlı içindekiler, sağda seçili adım.
 * Yeni sınavda yalnızca "Genel bilgiler" açık; kaydedilince sınavın kendi adresine geçilir ve diğer adımlar açılır.
 */
export function ExamEditorPage() {
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
  const { data, error, loading, reload } = useResource(load, 'Sınav yüklenemedi.')

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
    general: exam ? 'Kaydedildi' : 'Başlangıç',
    questions: exam ? `${exam.questionCount} soru` : 'Önce genel bilgiler',
    assignments: exam ? `${candidates} aday · ${reviewers} değerlendirici` : 'Önce genel bilgiler',
    risk: 'Platform geneli',
  }
  const phase = exam ? windowPhase(exam, now.getTime()) : null
  const current = STEPS.find((item) => item.key === step)!

  return (
    <div className="admin-page">
      <PageHeader
        breadcrumbs={[{ label: 'Yönetim' }, { label: 'Sınavlar', to: '/admin/exams' }]}
        title={isNew ? 'Yeni sınav' : (exam?.name ?? 'Sınav')}
        description={isNew ? 'Önce genel bilgileri kaydedin; ardından soruları ve atamaları ekleyin.' : current.hint}
        actions={phase ? <StatusBadge tone={PHASE_TONE[phase]} dot>{PHASE_LABEL[phase]}</StatusBadge> : null}
      />

      {error && !data ? (
        <Alert tone="danger" title="Sınav açılamadı">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : null}

      <div className="editor">
        <nav className="editor-steps" aria-label="Sınav adımları">
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
                      {index + 1}. {item.label}
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

        <section className="editor-body" aria-label={current.label}>
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
