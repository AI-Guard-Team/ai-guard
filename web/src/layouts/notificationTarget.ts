import type { NotificationItem } from '../api/management'

/** Bildirimin götürdüğü sayfa; ilgili ekranda iş tamamlanınca bildirim sunucudan kendiliğinden düşer. */
export function notificationTarget(item: NotificationItem): string {
  switch (item.kind) {
    case 'SessionAwaitingReview':
      return item.sessionId ? `/review/sessions/${item.sessionId}` : '/review'
    case 'ExamAwaitingReview':
      return '/admin/reports'
    case 'ExamWithoutQuestions':
      return item.examId ? `/admin/exams/${item.examId}/questions` : '/admin/exams'
    case 'ExamWithoutCandidates':
      return item.examId ? `/admin/exams/${item.examId}/assignments` : '/admin/exams'
    case 'InvitesExpired':
      return '/admin/users'
    case 'PhotoMissing':
      return '/profile/photo'
    default:
      return '/exams'
  }
}
