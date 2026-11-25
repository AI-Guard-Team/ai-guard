// Tembel yüklenen sayfa parçaları. App.tsx bunlardan React.lazy bileşenleri üretir.

export const pageLoaders = {
  setPassword: () => import('./pages/auth/SetPasswordPage').then((m) => ({ default: m.SetPasswordPage })),
  forgotPassword: () => import('./pages/auth/ForgotPasswordPage').then((m) => ({ default: m.ForgotPasswordPage })),
  resetPassword: () => import('./pages/auth/ResetPasswordPage').then((m) => ({ default: m.ResetPasswordPage })),
  profile: () => import('./pages/profile/ProfilePage').then((m) => ({ default: m.ProfilePage })),
  profilePhoto: () => import('./features/biometric/ReferencePhotoPage').then((m) => ({ default: m.ReferencePhotoPage })),
  examStart: () => import('./features/preflight/PreflightPage').then((m) => ({ default: m.PreflightPage })),
  myExams: () => import('./features/exams/MyExamsPage').then((m) => ({ default: m.MyExamsPage })),
  exam: () => import('./pages/ExamPage').then((m) => ({ default: m.ExamPage })),
  roleHome: () => import('./pages/RoleHomePage').then((m) => ({ default: m.RoleHomePage })),
}

/** Bütün sayfa parçalarını önceden indirir (testlerde ve ileride boşta önden yükleme için). */
export function preloadPages(): Promise<unknown> {
  return Promise.all(Object.values(pageLoaders).map((load) => load()))
}
