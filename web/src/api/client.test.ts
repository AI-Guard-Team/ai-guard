import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '../auth/tokenStore'
import { authHeader, jsonResponse, mockFetch } from '../test/fetch'
import { setLang } from '../i18n/lang'
import { ApiError, apiRequest } from './client'

afterEach(() => {
  vi.unstubAllGlobals()
  setLang('tr')
})

describe('apiRequest', () => {
  it('erişim belirtecini Authorization başlığına ekler ve JSON gövdesini döndürür', async () => {
    tokenStore.set({ accessToken: 'erisim-1', refreshToken: 'yenileme-1' })
    const fetchMock = mockFetch(() => jsonResponse(200, { ad: 'Şeyma' }))

    await expect(apiRequest('/api/me')).resolves.toEqual({ ad: 'Şeyma' })
    expect(authHeader(fetchMock.mock.calls[0][1]!)).toBe('Bearer erisim-1')
  })

  it('auth: false olduğunda belirteç göndermez ve 401de yenileme denemez', async () => {
    tokenStore.set({ accessToken: 'erisim-1', refreshToken: 'yenileme-1' })
    const fetchMock = mockFetch(() => jsonResponse(401))

    await expect(apiRequest('/api/auth/login', { method: 'POST', body: {}, auth: false })).rejects.toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(authHeader(fetchMock.mock.calls[0][1]!)).toBeUndefined()
  })

  it('401 alınca belirteci yeniler ve isteği yeni belirteçle bir kez tekrarlar', async () => {
    tokenStore.set({ accessToken: 'eski', refreshToken: 'yenileme-1' })
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/auth/refresh') return jsonResponse(200, { accessToken: 'yeni', refreshToken: 'yenileme-2' })
      return authHeader(init) === 'Bearer yeni' ? jsonResponse(200, { tamam: true }) : jsonResponse(401)
    })

    await expect(apiRequest('/api/exams')).resolves.toEqual({ tamam: true })
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(JSON.parse(fetchMock.mock.calls[1][1]!.body as string)).toEqual({ refreshToken: 'yenileme-1' })
    expect(tokenStore.get()).toEqual({ accessToken: 'yeni', refreshToken: 'yenileme-2' })
  })

  it('aynı anda 401 alan istekler için yalnızca bir yenileme isteği gönderir', async () => {
    tokenStore.set({ accessToken: 'eski', refreshToken: 'yenileme-1' })
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/auth/refresh') return jsonResponse(200, { accessToken: 'yeni', refreshToken: 'yenileme-2' })
      return authHeader(init) === 'Bearer yeni' ? jsonResponse(200, {}) : jsonResponse(401)
    })

    await Promise.all([apiRequest('/api/a'), apiRequest('/api/b'), apiRequest('/api/c')])

    const refreshCalls = fetchMock.mock.calls.filter(([url]) => url === '/api/auth/refresh')
    expect(refreshCalls).toHaveLength(1)
  })

  it('yenileme başarısız olursa oturumu kapatır ve 401 hatası fırlatır', async () => {
    tokenStore.set({ accessToken: 'eski', refreshToken: 'gecersiz' })
    mockFetch(() => jsonResponse(401))

    const error = await apiRequest('/api/exams').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 401, message: 'Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.' })
    expect(tokenStore.get()).toBeNull()
  })

  it('sunucunun "detail" mesajını kullanır', async () => {
    mockFetch(() => jsonResponse(400, { title: 'One or more validation errors occurred.', detail: 'Şifre en az 8 karakter olmalıdır.' }))

    await expect(apiRequest('/api/x', { auth: false })).rejects.toThrow('Şifre en az 8 karakter olmalıdır.')
  })

  it('mesaj yoksa duruma göre Türkçe varsayılan mesaj verir', async () => {
    mockFetch(() => jsonResponse(503))

    await expect(apiRequest('/api/x', { auth: false })).rejects.toThrow('Sunucuda bir hata oluştu. Lütfen daha sonra tekrar deneyin.')
  })

  it('arayüz dilini Accept-Language ile gönderir ve sunucunun İngilizce metnini gösterir (K-48)', async () => {
    setLang('en')
    const fetchMock = mockFetch(() => jsonResponse(401, { title: 'Sign-in failed.', detail: 'Incorrect email or password.' }))

    const error = (await apiRequest('/api/auth/login', { method: 'POST', body: {}, auth: false }).catch((e: unknown) => e)) as ApiError

    expect((fetchMock.mock.calls[0][1]!.headers as Record<string, string>)['Accept-Language']).toBe('en')
    expect(error.message).toBe('Incorrect email or password.')
    expect(error.title).toBe('Sign-in failed.')
  })

  it('ASP.NET varsayılan başlığını kullanıcıya göstermez', async () => {
    mockFetch(() => jsonResponse(400, { title: 'One or more validation errors occurred.' }))

    const error = (await apiRequest('/api/x', { auth: false }).catch((e: unknown) => e)) as ApiError

    expect(error.title).toBeUndefined()
    expect(error.message).toBe('Gönderilen bilgiler geçersiz.')
  })

  it('FormData gövdesinde Content-Type başlığı yazmaz', async () => {
    tokenStore.set({ accessToken: 'erisim-1', refreshToken: 'yenileme-1' })
    const fetchMock = mockFetch(() => jsonResponse(204))
    const body = new FormData()
    body.append('photo', new Blob(['a'], { type: 'image/jpeg' }), 'a.jpg')

    await apiRequest('/api/me/photo', { method: 'POST', body })

    const init = fetchMock.mock.calls[0][1]!
    expect(init.body).toBeInstanceOf(FormData)
    expect((init.headers as Record<string, string>)['Content-Type']).toBeUndefined()
    expect(authHeader(init)).toBe('Bearer erisim-1')
  })

  it('ağ hatasında 0 durum kodlu anlaşılır bir hata verir', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))))

    await expect(apiRequest('/api/x', { auth: false })).rejects.toMatchObject({
      status: 0,
      message: 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.',
    })
  })
})
