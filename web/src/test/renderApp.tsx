import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import App from '../App'
import { AuthProvider } from '../auth/AuthProvider'
import type { Role } from '../auth/roles'
import { tokenStore } from '../auth/tokenStore'
import { makeAccessToken } from './tokens'

export function signInAs(role: Role, name = 'Test Kullanıcı'): void {
  tokenStore.set({ accessToken: makeAccessToken({ sub: 'kullanici-1', role, name }), refreshToken: 'yenileme' })
}

export function renderApp(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
}
