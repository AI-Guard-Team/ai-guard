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

describe('giriş', () => {
  it('boş formda alan hatalarını gösterir ve istek atmaz', async () => {
    const fetchMock = mockFetch(() => jsonResponse(500))
    renderApp('/login')

    await userEvent.click(screen.getByRole('button', { name: 'Giriş' }))

    expect(screen.getByText('E-posta zorunludur.')).toBeInTheDocument()
    expect(screen.getByText('Şifre zorunludur.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('doğru bilgilerle role göre ana sayfaya gider', async () => {
    const accessToken = makeAccessToken({ sub: 'aday-1', role: 'Candidate', name: 'Ayşe Demir' })
    const fetchMock = mockFetch(() => jsonResponse(200, { accessToken, refreshToken: 'yenileme' }))
    renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş' }))

    expect(await screen.findByRole('heading', { name: 'Sınavlarım' })).toBeInTheDocument()
    expect(tokenStore.get()?.accessToken).toBe(accessToken)
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({
      email: 'aday@aiguard.local',
      password: 'Sifre123!',
    })
  })

  it('girişten önce açılmak istenen sayfaya döner', async () => {
    const accessToken = makeAccessToken({ sub: 'aday-1', role: 'Candidate' })
    mockFetch(() => jsonResponse(200, { accessToken, refreshToken: 'yenileme' }))
    renderApp('/login', { from: '/profile' })

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş' }))

    expect(await screen.findByRole('heading', { name: 'Profil' })).toBeInTheDocument()
  })

  it('hatalı bilgide anlaşılır mesaj gösterir', async () => {
    mockFetch(() => jsonResponse(401, { detail: 'Geçersiz.' }))
    renderApp('/login')

    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'yanlis')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya şifre hatalı.')
  })
})

describe('şifre belirleme', () => {
  it('kod yoksa bağlantının bozuk olduğunu söyler', () => {
    renderApp('/set-password')

    expect(screen.getByText(/bağlantıyı açın/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Yeni şifre')).not.toBeInTheDocument()
  })

  it('şifreler farklıysa istek atmaz', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    renderApp('/set-password?token=davet-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'BirSifre')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'BaskaSifre')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }))

    expect(screen.getByText('Şifreler aynı olmalıdır.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('davet kodu ve şifreyi gönderip girişe yönlendirir', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    renderApp('/set-password?token=davet-1')

    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }))

    expect(await screen.findByRole('heading', { name: 'Şifre belirlendi' })).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/set-password')
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({ token: 'davet-1', password: 'YeniSifre1' })
  })
})

describe('profil ve şifre değiştirme', () => {
  it('belirteçteki adı, e-postayı ve rolü gösterir', () => {
    tokenStore.set({
      accessToken: makeAccessToken({
        sub: 'aday-1',
        role: 'Candidate',
        name: 'Ayşe Demir',
        email: 'aday@aiguard.local',
      }),
      refreshToken: 'yenileme',
    })
    renderApp('/profile')

    expect(screen.getByText('Ayşe Demir')).toBeInTheDocument()
    expect(screen.getByText('aday@aiguard.local')).toBeInTheDocument()
    expect(screen.getByText('Aday')).toBeInTheDocument()
  })

  it('yeni şifreyi mevcut şifreyle birlikte gönderir', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Reviewer', 'Mert Yılmaz')
    renderApp('/profile')

    await userEvent.type(screen.getByLabelText('Mevcut şifre'), 'EskiSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre'), 'YeniSifre1')
    await userEvent.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'YeniSifre1')
    await userEvent.click(screen.getByRole('button', { name: 'Şifreyi güncelle' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Şifreniz güncellendi.')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/change-password')
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({
      currentPassword: 'EskiSifre1',
      newPassword: 'YeniSifre1',
    })
  })
})
