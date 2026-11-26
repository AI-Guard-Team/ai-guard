import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/global.css'
import { initLang } from './i18n/lang'
import { initTheme } from './lib/theme'
import { Root } from './Root'

// Tema ve dil ilk çizimden önce: sayfa yanlış renkte ya da yanlış dilde görünüp sonra değişmesin (K-42, K-46).
initTheme()
initLang()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Root />
  </StrictMode>,
)
