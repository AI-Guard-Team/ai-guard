import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router'
import { HomeRedirect, RequireAuth } from './auth/RequireAuth'
import { RouteFallback } from './components/RouteFallback'
import { AppShell } from './layouts/AppShell'
import { LoginPage } from './pages/auth/LoginPage'
import { NotFoundPage, SessionExpiredPage } from './pages/errors/ErrorPages'
import { pageLoaders as loaders } from './pageLoaders'

// Giriş ekranı ilk açılışta hemen gelsin diye doğrudan yükleniyor; diğer sayfalar açıldıkça ayrı parça
// olarak iniyor. Böylece adayın giriş sayfası sınav ve kamera kodunu indirmek zorunda kalmıyor.
const SetPasswordPage = lazy(loaders.setPassword)
const ForgotPasswordPage = lazy(loaders.forgotPassword)
const ResetPasswordPage = lazy(loaders.resetPassword)
const ProfilePage = lazy(loaders.profile)
const ReferencePhotoPage = lazy(loaders.profilePhoto)
const PreflightPage = lazy(loaders.examStart)
const MyExamsPage = lazy(loaders.myExams)
const ExamPage = lazy(loaders.exam)
const RoleHomePage = lazy(loaders.roleHome)

export default function App() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/set-password" element={<SetPasswordPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/session-expired" element={<SessionExpiredPage />} />

        <Route element={<RequireAuth />}>
          <Route element={<AppShell />}>
            <Route index element={<HomeRedirect />} />
            <Route path="/profile" element={<ProfilePage />} />

            <Route element={<RequireAuth roles={['Admin']} />}>
              <Route path="/admin/*" element={<RoleHomePage title="Yönetim" />} />
            </Route>
            <Route element={<RequireAuth roles={['Reviewer']} />}>
              <Route path="/review/*" element={<RoleHomePage title="İncelemeler" />} />
            </Route>
            <Route element={<RequireAuth roles={['Candidate']} />}>
              <Route path="/profile/photo" element={<ReferencePhotoPage />} />
              <Route path="/exams/:sessionId/start" element={<PreflightPage />} />
              <Route path="/exams/:sessionId" element={<ExamPage />} />
              <Route path="/exams" element={<MyExamsPage />} />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
