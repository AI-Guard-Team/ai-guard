import { act, render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from '../App'
import { preloadPages } from '../pageLoaders'
import { AuthProvider } from '../auth/AuthProvider'
import type { Role } from '../auth/roles'
import { tokenStore } from '../auth/tokenStore'
import { ToastProvider } from '../components/ToastProvider'
import { useLang } from '../i18n/lang'
import { makeAccessToken } from './tokens'

export function signInAs(role: Role, name = 'Test Kullanıcı'): void {
  tokenStore.set({ accessToken: makeAccessToken({ sub: 'kullanici-1', role, name }), refreshToken: 'yenileme' })
}

/**
 * Uygulamayı verilen adreste açar ve tembel yüklenen sayfa parçaları (React.lazy) inene kadar bekler.
 */
export async function renderApp(path: string, state?: unknown) {
  const searchAt = path.indexOf('?')
  const entry =
    state === undefined
      ? path
      : { pathname: searchAt === -1 ? path : path.slice(0, searchAt), search: searchAt === -1 ? '' : path.slice(searchAt), state }

  // Parçalar önceden indirilir; act içinde çizilince Suspense aynı turda çözülür (sahte zamanlayıcıyla da çalışır).
  await preloadPages()
  // main.tsx'teki gibi: dil değişince bütün ağaç yeniden çizilir.
  function Root() {
    useLang()
    return (
      <MemoryRouter initialEntries={[entry]}>
        <ToastProvider>
          <AuthProvider>
            <App />
          </AuthProvider>
        </ToastProvider>
      </MemoryRouter>
    )
  }
  let result!: ReturnType<typeof render>
  await act(async () => {
    result = render(<Root />)
  })
  return result
}
