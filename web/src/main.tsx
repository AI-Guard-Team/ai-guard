import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import './styles/global.css'
import App from './App.tsx'
import { AuthProvider } from './auth/AuthProvider'
import { ToastProvider } from './components/ToastProvider'
import { initTheme } from './lib/theme'

// Tema ilk çizimden önce: sayfa yanlış renkte görünüp sonra değişmesin (K-42).
initTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <ToastProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </ToastProvider>
    </BrowserRouter>
  </StrictMode>,
)
