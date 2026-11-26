import { BrowserRouter } from 'react-router'
import App from './App.tsx'
import { AuthProvider } from './auth/AuthProvider'
import { ToastProvider } from './components/ToastProvider'
import { useLang } from './i18n/lang'

/**
 * Uygulamanın kökü. Dil değişince bütün ağaç yeniden çizilir (K-46); durum (form alanları, açık pencereler) korunur,
 * düz işlevlerden okunan etiketler de güncel dile geçer.
 */
export function Root() {
  useLang()
  return (
    <BrowserRouter>
      <ToastProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </ToastProvider>
    </BrowserRouter>
  )
}
