// Tembel yüklenen sayfa parçaları. App.tsx bunlardan React.lazy bileşenleri üretir.

export const pageLoaders = {
  setPassword: () => import('./pages/auth/SetPasswordPage').then((m) => ({ default: m.SetPasswordPage })),
  forgotPassword: () => import('./pages/auth/ForgotPasswordPage').then((m) => ({ default: m.ForgotPasswordPage })),
  resetPassword: () => import('./pages/auth/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage })),
  profile: () => import('./pages/profile/ProfilePage').then((m) => ({ default: m.ProfilePage })),
  profilePhoto: () => import('./features/biometric/ReferencePhotoPage').then((m) => ({ default: m.ReferencePhotoPage })),
  examStart: () => import('./features/preflight/PreflightPage').then((m) => ({ default: m.PreflightPage })),
  myExams: () => import('./features/exams/MyExamsPage').then((m) => ({ default: m.MyExamsPage })),
  exam: () => import('./features/exam/ExamPage').then((m) => ({ default: m.ExamPage })),
  reviewerExams: () => import('./features/review/ReviewerExamsPage').then((m) => ({ default: m.ReviewerExamsPage })),
  reviewPanel: () => import('./features/review/ReviewPanelPage').then((m) => ({ default: m.ReviewPanelPage })),
  reviewSession: () => import('./features/review/SessionDetailPage').then((m) => ({ default: m.SessionDetailPage })),
  candidateReport: () => import('./features/review/CandidateReportPage').then((m) => ({ default: m.CandidateReportPage })),
  roleHome: () => import('./pages/RoleHomePage').then((m) => ({ default: m.RoleHomePage })),
}

/** Bütün sayfa parçalarını önceden indirir (testlerde ve ileride boşta önden yükleme için). */
export function preloadPages(): Promise<unknown> {
  return Promise.all(Object.values(pageLoaders).map((load) => load()))
}
