import { Route, Routes } from 'react-router'
import { HomeRedirect, RequireAuth } from './auth/RequireAuth'
import { Layout } from './components/Layout'
import { ExamStartPage } from './pages/ExamStartPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { ProfilePage } from './pages/ProfilePage'
import { ProfilePhotoPage } from './pages/ProfilePhotoPage'
import { RoleHomePage } from './pages/RoleHomePage'
import { SetPasswordPage } from './pages/SetPasswordPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/set-password" element={<SetPasswordPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<Layout />}>
          <Route index element={<HomeRedirect />} />
          <Route path="/profile" element={<ProfilePage />} />

          <Route element={<RequireAuth roles={['Admin']} />}>
            <Route path="/admin/*" element={<RoleHomePage title="Yönetim" />} />
          </Route>
          <Route element={<RequireAuth roles={['Reviewer']} />}>
            <Route path="/review/*" element={<RoleHomePage title="İncelemeler" />} />
          </Route>
          <Route element={<RequireAuth roles={['Candidate']} />}>
            <Route path="/profile/photo" element={<ProfilePhotoPage />} />
            <Route path="/exams/:sessionId/start" element={<ExamStartPage />} />
            <Route path="/exams/*" element={<RoleHomePage title="Sınavlarım" />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
