import { tokenStore, type Tokens } from '../auth/tokenStore'
import { apiRequest, type TokenResponse } from './client'

// Uç adları E2 kartından alınmıştır; değişmesi gerekirse önce ekibe sorulur.
export const AUTH_ENDPOINTS = {
  login: '/api/auth/login',
  logout: '/api/auth/logout',
  setPassword: '/api/auth/set-password',
  // E2 kartında tanımlı değil; ad Esma ile netleştirilecek.
  changePassword: '/api/auth/change-password',
} as const

export async function login(email: string, password: string): Promise<Tokens> {
  const data = await apiRequest<TokenResponse>(AUTH_ENDPOINTS.login, {
    method: 'POST',
    body: { email, password },
    auth: false,
  })
  return { accessToken: data.accessToken, refreshToken: data.refreshToken ?? null }
}

export function setPassword(token: string, password: string): Promise<void> {
  return apiRequest(AUTH_ENDPOINTS.setPassword, { method: 'POST', body: { token, password }, auth: false })
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiRequest(AUTH_ENDPOINTS.changePassword, { method: 'POST', body: { currentPassword, newPassword } })
}

export async function logout(): Promise<void> {
  const refreshToken = tokenStore.get()?.refreshToken
  // Arayüz sunucunun cevabını beklemeden hemen çıkış yapmış duruma geçer.
  tokenStore.clear()
  if (!refreshToken) return
  try {
    await apiRequest(AUTH_ENDPOINTS.logout, { method: 'POST', body: { refreshToken }, auth: false })
  } catch {
    // Sunucu isteği reddetse de yerel oturum kapanmıştır; kullanıcıya hata gösterilmez.
  }
}
