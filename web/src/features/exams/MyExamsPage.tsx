import { ArrowRight, ClipboardList } from 'lucide-react'
import { Link } from 'react-router'
import { listExams, type ExamSummary } from '../../api/exams'
import { getBiometricStatus, type BiometricStatus } from '../../api/profile'
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
      <PageHeader title="Sınavlarım" description="Size atanmış sınavlar ve oturum durumunuz." />

      {data && !data.biometric.enrolled ? (
        <Alert tone="warning" title="Kayıt fotoğrafınız yok">
          <p>Sınav başında kimliğiniz bu fotoğrafla doğrulanır. Fotoğraf yüklemeden hiçbir sınava başlayamazsınız.</p>
          <Link className="alert-link" to="/profile/photo">
            Kayıt fotoğrafı yükle
            <ArrowRight size={16} aria-hidden="true" />
          </Link>
        </Alert>
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
