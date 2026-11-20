import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from '../App'
import { AuthProvider } from '../auth/AuthProvider'
import type { Role } from '../auth/roles'
import { tokenStore } from '../auth/tokenStore'
import { ToastProvider } from '../components/ToastProvider'
import { makeAccessToken } from './tokens'

export function signInAs(role: Role, name = 'Test Kullanıcı'): void {
  tokenStore.set({ accessToken: makeAccessToken({ sub: 'kullanici-1', role, name }), refreshToken: 'yenileme' })
}

export function renderApp(path: string, state?: unknown) {
  const searchAt = path.indexOf('?')
  const entry =
    state === undefined
      ? path
      : { pathname: searchAt === -1 ? path : path.slice(0, searchAt), search: searchAt === -1 ? '' : path.slice(searchAt), state }

  return render(
    <MemoryRouter initialEntries={[entry]}>
      <ToastProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </ToastProvider>
    </MemoryRouter>,
  )
}
