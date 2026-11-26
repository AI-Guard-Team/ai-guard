import { ArrowRight, Camera, ClipboardList } from 'lucide-react'
import { Link } from 'react-router'
import { listExams, type ExamSummary } from '../../api/exams'
import { getBiometricStatus, type BiometricStatus } from '../../api/profile'
import { ActionBanner } from '../../components/ActionBanner'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { PageHeader } from '../../components/PageHeader'
import { Skeleton } from '../../components/Skeleton'
import { useNow } from '../../hooks/useNow'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { defineMessages, useMessages } from '../../i18n/messages'
import { ExamCard } from './ExamCard'
import { examStatus } from './examStatus'
import './MyExamsPage.css'

const messages = defineMessages({
  tr: {
    failed: 'Sınavlarınız yüklenemedi.',
    title: 'Sınavlarım',
    description: 'Size atanan sınavlar, açılış saatleri ve durumları.',
    photoTitle: 'Kayıt fotoğrafınızı yükleyin',
    photoAction: 'Fotoğraf yükle',
    photoText: 'Sınav başında kimliğiniz bu fotoğrafla doğrulanır; yüklemeden sınava başlayamazsınız.',
    listFailed: 'Sınavlar yüklenemedi',
    loading: 'Sınavlar yükleniyor',
    emptyTitle: 'Size atanmış sınav yok',
    emptyText: 'Kurumunuz sizi bir sınava atadığında burada görünür. Bir sınava atanmanız gerektiğini düşünüyorsanız yöneticinize başvurun.',
  },
  en: {
    failed: 'Your exams could not be loaded.',
    title: 'My exams',
    description: 'Exams assigned to you, their opening times and status.',
    photoTitle: 'Upload your reference photo',
    photoAction: 'Upload photo',
    photoText: 'Your identity is checked against this photo at the start of the exam; you cannot start without it.',
    listFailed: 'Exams could not be loaded',
    loading: 'Loading exams',
    emptyTitle: 'No exams assigned to you',
    emptyText: 'When your institution assigns you to an exam, it appears here. If you think you should be assigned, contact your administrator.',
  },
})

interface MyExamsData {
  exams: ExamSummary[]
  biometric: BiometricStatus
}

// İki istek aynı anda gider; sayfa ikisi gelince bir kez çizilir.
const loadMyExams = async (signal: AbortSignal): Promise<MyExamsData> => {
  const [exams, biometric] = await Promise.all([listExams(signal), getBiometricStatus(signal)])
  return { exams, biometric }
}

/** Adayın atandığı sınavlar (Stitch ekran 7; GET /api/exams, GET /api/me/biometric). */
export function MyExamsPage() {
  const m = useMessages(messages)
  const c = useMessages(common)
  // Modül düzeyindeki fonksiyon zaten sabit; useCallback gerekmez.
  const { data, error, loading, reload } = useResource(loadMyExams, m.failed)
  const now = useNow()

  return (
    <>
      <PageHeader title={m.title} description={m.description} />

      {data && !data.biometric.enrolled ? (
        <ActionBanner
          icon={Camera}
          title={m.photoTitle}
          action={
            <Link className="button button-primary button-md" to="/profile/photo">
              {m.photoAction}
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          }
        >
          {m.photoText}
        </ActionBanner>
      ) : null}

      {error && !data ? (
        <Alert tone="danger" title={m.listFailed}>
          <p>{error}</p>
          <Button variant="secondary" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : loading && !data ? (
        <div className="exam-grid" aria-busy="true" aria-label={m.loading}>
          {[0, 1, 2].map((key) => (
            <Skeleton key={key} height="22rem" radius="lg" />
          ))}
        </div>
      ) : data && data.exams.length === 0 ? (
        <EmptyState icon={ClipboardList} title={m.emptyTitle}>
          <p>{m.emptyText}</p>
        </EmptyState>
      ) : data ? (
        <div className="exam-grid">
          {data.exams.map((exam) => (
            <ExamCard key={exam.id} exam={exam} status={examStatus(exam, data.biometric.enrolled, now)} />
          ))}
        </div>
      ) : null}
    </>
  )
}
