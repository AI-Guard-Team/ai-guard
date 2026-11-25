import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '../auth/tokenStore'
import { jsonResponse, mockFetch } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'
import { makeAccessToken } from '../test/tokens'

afterEach(() => {
  vi.unstubAllGlobals()
})

function body(call: unknown[]): unknown {
  return JSON.parse((call[1] as RequestInit).body as string)
}

describe('giriş', () => {
  it('boş formda alan hatalarını gösterir ve istek atmaz', async () => {
    const fetchMock = mockFetch(() => jsonResponse(500))
    await renderApp('/login')

    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(screen.getByText('E-posta zorunludur.')).toBeInTheDocument()
    expect(screen.getByText('Şifre zorunludur.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('doğru bilgilerle role göre ana sayfaya gider', async () => {
    const accessToken = makeAccessToken({ sub: 'aday-1', role: 'Candidate', name: 'Ayşe Demir' })
    const fetchMock = mockFetch((url) => (url.endsWith('/login') ? jsonResponse(200, { accessToken, refreshToken: 'yenileme' }) : jsonResponse(200, [])))
    await renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sınavlarım' })).toBeInTheDocument()
    expect(tokenStore.get()?.accessToken).toBe(accessToken)
    expect(body(fetchMock.mock.calls[0])).toEqual({ email: 'aday@aiguard.local', password: 'Sifre123!' })
  })

  it('girişten önce açılmak istenen sayfaya döner', async () => {
    const accessToken = makeAccessToken({ sub: 'aday-1', role: 'Candidate' })
    mockFetch((url) => (url.endsWith('/login') ? jsonResponse(200, { accessToken, refreshToken: 'yenileme' }) : jsonResponse(200, [])))
    await renderApp('/login', { from: '/profile' })

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Profil' })).toBeInTheDocument()
  })

  it.each(['//kotu.example', '/\\kotu.example', 'https://kotu.example'])('dış adrese (%s) yönlendirmez', async (from) => {
    const accessToken = makeAccessToken({ sub: 'aday-1', role: 'Candidate' })
    mockFetch((url) => (url.endsWith('/login') ? jsonResponse(200, { accessToken, refreshToken: 'yenileme' }) : jsonResponse(200, [])))
    await renderApp('/login', { from })

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sınavlarım' })).toBeInTheDocument()
  })

  it('hatalı bilgide anlaşılır mesaj gösterir', async () => {
    mockFetch(() => jsonResponse(401, { detail: 'Geçersiz.' }))
    await renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'yanlis')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya şifre hatalı.')
  })

  it('hesap kilitliyse sunucunun kalan süre mesajını uyarı olarak gösterir', async () => {
    mockFetch(() => jsonResponse(429, { detail: 'Çok fazla hatalı giriş denemesi. 14 dakika sonra tekrar deneyin.' }))
    await renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'yanlis')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Hesap geçici olarak kilitlendi')
    expect(alert).toHaveTextContent('14 dakika sonra tekrar deneyin.')
  })

  it('şifreyi göster düğmesi alanın türünü değiştirir', async () => {
    await renderApp('/login')
    const password = screen.getByLabelText('Şifre')

    expect(password).toHaveAttribute('type', 'password')
    await userEvent.click(screen.getByRole('button', { name: 'Şifre: göster' }))
    expect(password).toHaveAttribute('type', 'text')
  })
})

describe('hesap etkinleştirme', () => {
  it('kod yoksa bağlantının bozuk olduğunu söyler', async () => {
    await renderApp('/set-password')

    expect(screen.getByText(/bağlantıyı açın/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Yeni şifre')).not.toBeInTheDocument()
  })

  it('şifreler farklıysa istek atmaz', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    await renderApp('/set-password?token=davet-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'BirSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'BaskaSifre')
    await userEvent.click(screen.getByRole('button', { name: 'Hesabı etkinleştir' }))

    expect(screen.getByText('Şifreler aynı olmalıdır.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sunucu kuralına uymayan kısa şifreyi göndermez', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    await renderApp('/set-password?token=davet-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'kisa')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'kisa')
    await userEvent.click(screen.getByRole('button', { name: 'Hesabı etkinleştir' }))

    expect(screen.getByText('Şifre en az 8 karakter olmalıdır.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('davet kodu ve şifreyi gönderip girişe yönlendirir', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    await renderApp('/set-password?token=davet-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    expect(screen.getByRole('list', { name: 'Şifre kuralları' })).toHaveTextContent('İki alan aynı (sağlandı)')
    await userEvent.click(screen.getByRole('button', { name: 'Hesabı etkinleştir' }))

    expect(await screen.findByRole('heading', { name: 'Hesabınız etkinleştirildi' })).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/set-password')
    expect(body(fetchMock.mock.calls[0])).toEqual({ token: 'davet-1', password: 'YeniSifre1' })
  })
})

describe('şifremi unuttum', () => {
  it('girişten açılır ve hesap olsun olmasın aynı mesajı gösterir', async () => {
    const fetchMock = mockFetch(() => jsonResponse(202))
    await renderApp('/login')

    await userEvent.click(screen.getByRole('link', { name: 'Şifremi unuttum' }))
    await screen.findByRole('heading', { name: 'Şifrenizi mi unuttunuz?' })
    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.click(screen.getByRole('button', { name: 'Sıfırlama bağlantısı gönder' }))

    expect(await screen.findByRole('heading', { name: 'E-postanızı kontrol edin' })).toBeInTheDocument()
    expect(screen.getByText(/kayıtlı bir hesap varsa/)).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/forgot-password')
    expect(body(fetchMock.mock.calls[0])).toEqual({ email: 'aday@aiguard.local' })
  })

  it('geçersiz e-postada istek atmaz', async () => {
    const fetchMock = mockFetch(() => jsonResponse(202))
    await renderApp('/forgot-password')

    await userEvent.type(screen.getByLabelText('E-posta'), 'adres-degil')
    await userEvent.click(screen.getByRole('button', { name: 'Sıfırlama bağlantısı gönder' }))

    expect(screen.getByText('Geçerli bir e-posta adresi girin.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('şifre sıfırlama', () => {
  it('kod yoksa yeni bağlantı istemeye yönlendirir', async () => {
    await renderApp('/reset-password')

    expect(screen.getByRole('link', { name: 'Yeni bağlantı iste' })).toHaveAttribute('href', '/forgot-password')
  })

  it('bağlantıdaki kod ve yeni şifreyi gönderir', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    await renderApp('/reset-password?token=sifirla-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi yenile' }))

    expect(await screen.findByRole('heading', { name: 'Şifreniz yenilendi' })).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/reset-password')
    expect(body(fetchMock.mock.calls[0])).toEqual({ token: 'sifirla-1', password: 'YeniSifre1' })
  })

  it('süresi dolmuş bağlantıda sunucunun mesajını gösterir', async () => {
    mockFetch(() => jsonResponse(400, { detail: 'Sıfırlama bağlantısı geçersiz, kullanılmış veya süresi dolmuş. Yeni bir bağlantı isteyin.' }))
    await renderApp('/reset-password?token=eski')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi yenile' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('süresi dolmuş')
  })
})

describe('profil ve şifre değiştirme', () => {
  it('belirteçteki adı, e-postayı ve rolü gösterir', async () => {
    tokenStore.set({
      accessToken: makeAccessToken({ sub: 'aday-1', role: 'Candidate', name: 'Ayşe Demir', email: 'aday@aiguard.local' }),
      refreshToken: 'yenileme',
    })
    await renderApp('/profile')

    const facts = screen.getByRole('heading', { name: 'Hesap bilgileri' }).closest('section')!
    expect(facts).toHaveTextContent('Ayşe Demir')
    expect(facts).toHaveTextContent('aday@aiguard.local')
    expect(facts).toHaveTextContent('Aday')
    expect(screen.getByRole('navigation', { name: 'Profil bölümleri' })).toBeInTheDocument()
  })

  it('aday olmayan rolde kayıt fotoğrafı sekmesi çıkmaz', async () => {
    signInAs('Reviewer', 'Mert Yılmaz')
    await renderApp('/profile')

    expect(screen.queryByRole('navigation', { name: 'Profil bölümleri' })).not.toBeInTheDocument()
  })

  it('yeni şifreyi gönderir ve aynı sekmede yeni şifreyle oturumu yeniler', async () => {
    const renewed = makeAccessToken({ sub: 'kullanici-1', role: 'Reviewer', name: 'Mert Yılmaz', email: 'mert@aiguard.local' })
    const fetchMock = mockFetch((url) =>
      url.endsWith('/api/auth/login') ? jsonResponse(200, { accessToken: renewed, refreshToken: 'yeni' }) : jsonResponse(204),
    )
    tokenStore.set({
      accessToken: makeAccessToken({ sub: 'kullanici-1', role: 'Reviewer', name: 'Mert Yılmaz', email: 'mert@aiguard.local' }),
      refreshToken: 'yenileme',
    })
    await renderApp('/profile')

    await userEvent.type(screen.getByLabelText('Mevcut şifre'), 'EskiSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi güncelle' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Şifreniz güncellendi.')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/change-password')
    expect(body(fetchMock.mock.calls[0])).toEqual({ currentPassword: 'EskiSifre1', newPassword: 'YeniSifre1' })
    expect(body(fetchMock.mock.calls[1])).toEqual({ email: 'mert@aiguard.local', password: 'YeniSifre1' })
    expect(tokenStore.get()?.refreshToken).toBe('yeni')
  })

  it('yeni şifre mevcutla aynıysa göndermez', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Admin')
    await renderApp('/profile')

    await userEvent.type(screen.getByLabelText('Mevcut şifre'), 'AyniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'AyniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'AyniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi güncelle' }))

    expect(screen.getByText('Yeni şifre mevcut şifreyle aynı olamaz.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
