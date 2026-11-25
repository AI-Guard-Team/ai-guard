import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { listReviewExams, type ExamSummary } from '../api/review'
import { ApiError } from '../api/client'
import { FormAlert } from '../components/AuthCard'
import { Table } from '../components/Table'
import { formatDateTime } from '../review/labels'

/** Değerlendiricinin atandığı sınavlar; bir sınava girince oturum listesi açılır (S5). */
export function ReviewExamsPage() {
  const [exams, setExams] = useState<ExamSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    listReviewExams()
      .then((list) => {
        if (!controller.signal.aborted) setExams(list)
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(reason instanceof ApiError ? reason.message : 'Sınavlar yüklenemedi.')
      })
    return () => controller.abort()
  }, [])

  return (
    <section className="card review-wide">
      <h1>İncelemeler</h1>
      <p className="auth-lead">İncelemek için atandığınız bir sınavı seçin.</p>
      <FormAlert message={error} />
      {exams === null ? (
        error ? null : <p>Yükleniyor…</p>
      ) : (
        <Table
          rows={exams}
          rowKey={(exam) => exam.id}
          empty="Size atanmış bir sınav yok."
          columns={[
            {
              key: 'name',
              header: 'Sınav',
              render: (exam) => <Link to={`/review/exams/${exam.id}`}>{exam.name}</Link>,
            },
            { key: 'start', header: 'Başlangıç', render: (exam) => formatDateTime(exam.windowStartAt) },
            { key: 'end', header: 'Bitiş', render: (exam) => formatDateTime(exam.windowEndAt) },
          ]}
        />
      )}
    </section>
  )
}
