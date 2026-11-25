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
import { ExamCard } from './ExamCard'
import { examStatus } from './examStatus'
import './MyExamsPage.css'

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
  // Modül düzeyindeki fonksiyon zaten sabit; useCallback gerekmez.
  const { data, error, loading, reload } = useResource(loadMyExams, 'Sınavlarınız yüklenemedi.')
  const now = useNow()

  return (
    <>
      <PageHeader title="Sınavlarım" description="Size atanan sınavlar, açılış saatleri ve durumları." />

      {data && !data.biometric.enrolled ? (
        <ActionBanner
          icon={Camera}
          title="Kayıt fotoğrafınızı yükleyin"
          action={
            <Link className="button button-primary button-md" to="/profile/photo">
              Fotoğraf yükle
              <ArrowRight size={18} aria-hidden="true" />
            </Link>
          }
        >
          Sınav başında kimliğiniz bu fotoğrafla doğrulanır; yüklemeden sınava başlayamazsınız.
        </ActionBanner>
      ) : null}

      {error && !data ? (
        <Alert tone="danger" title="Sınavlar yüklenemedi">
          <p>{error}</p>
          <Button variant="secondary" onClick={reload}>
            Yeniden dene
          </Button>
        </Alert>
      ) : loading && !data ? (
        <div className="exam-grid" aria-busy="true" aria-label="Sınavlar yükleniyor">
          {[0, 1, 2].map((key) => (
            <Skeleton key={key} height="22rem" radius="lg" />
          ))}
        </div>
      ) : data && data.exams.length === 0 ? (
        <EmptyState icon={ClipboardList} title="Size atanmış sınav yok">
          <p>Kurumunuz sizi bir sınava atadığında burada görünür. Bir sınava atanmanız gerektiğini düşünüyorsanız yöneticinize başvurun.</p>
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
