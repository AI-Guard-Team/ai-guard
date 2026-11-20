import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '../auth/tokenStore'
import { jsonResponse, mockFetch } from '../test/fetch'
import { login, logout } from './auth'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('login', () => {
  it('e-posta ve şifreyi gönderip belirteçleri döndürür', async () => {
    const fetchMock = mockFetch(() => jsonResponse(200, { accessToken: 'erisim', refreshToken: 'yenileme' }))

    await expect(login('aday@aiguard.local', 'Sifre123!')).resolves.toEqual({ accessToken: 'erisim', refreshToken: 'yenileme' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/auth/login')
    expect(JSON.parse(init!.body as string)).toEqual({ email: 'aday@aiguard.local', password: 'Sifre123!' })
  })
})

describe('logout', () => {
  it('yenileme belirtecini sunucuya gönderir ve oturumu kapatır', async () => {
    tokenStore.set({ accessToken: 'erisim', refreshToken: 'yenileme' })
    const fetchMock = mockFetch(() => jsonResponse(204))

    await logout()

    expect(tokenStore.get()).toBeNull()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/logout')
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({ refreshToken: 'yenileme' })
  })

  it('sunucu hata verse de yerel oturumu kapatır', async () => {
    tokenStore.set({ accessToken: 'erisim', refreshToken: 'yenileme' })
    mockFetch(() => jsonResponse(500))

    await expect(logout()).resolves.toBeUndefined()
    expect(tokenStore.get()).toBeNull()
  })
})
