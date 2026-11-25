import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from './auth/tokenStore'
import { jsonResponse, mockFetch } from './test/fetch'
import { renderApp, signInAs } from './test/renderApp'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('yönlendirme', () => {
  it('giriş yapmamış kullanıcıyı giriş sayfasına gönderir', async () => {
    await renderApp('/admin')

    expect(screen.getByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
  })

  it.each([
    ['Admin', 'Sınavlar'],
    ['Reviewer', 'İncelemeler'],
    ['Candidate', 'Sınavlarım'],
  ] as const)('%s kökten kendi ana sayfasına yönlenir', async (role, heading) => {
    signInAs(role)
    await renderApp('/')

    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it('başka rolün sayfasında 403 gösterir ve menüyü korur', async () => {
    signInAs('Candidate')
    await renderApp('/admin')

    expect(screen.getByRole('heading', { name: 'Bu sayfaya erişim yetkiniz yok' })).toBeInTheDocument()
    expect(screen.getByText('403')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Ana menü' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ana sayfaya dön' })).toHaveAttribute('href', '/exams')
  })

  it('menüde yalnızca kullanıcının rolüne ait bağlantılar görünür', async () => {
    signInAs('Reviewer', 'Mert Yılmaz')
    await renderApp('/review')

    const nav = screen.getByRole('navigation', { name: 'Ana menü' })
    expect(nav).toHaveTextContent('İncelemeler')
    expect(nav).toHaveTextContent('Profil')
    expect(nav).not.toHaveTextContent('Yönetim')
    expect(nav).not.toHaveTextContent('Sınavlarım')
    expect(within(nav).getByRole('link', { name: 'İncelemeler' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByText('Mert Yılmaz')).toBeInTheDocument()
    expect(screen.getByText('Değerlendirici')).toBeInTheDocument()
  })

  it('Çıkış butonu oturumu kapatıp giriş sayfasına götürür', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Admin')
    await renderApp('/admin')

    await userEvent.click(screen.getByRole('button', { name: 'Çıkış yap' }))

    expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument()
    expect(tokenStore.get()).toBeNull()
    expect(tokenStore.endReason()).toBe('signout')
    expect(fetchMock.mock.calls.map((call) => call[0])).toContain('/api/auth/logout') // yönetici ana sayfası önce sınavları istiyor
  })

  it('yenileme reddedilince "oturum süresi doldu" ekranına götürür, giriş sonrası aynı sayfaya döner', async () => {
    // Erişim belirteci 401 alır, yenileme de reddedilir: kullanıcı neden çıkarıldığını görmeli.
    const accessToken = (await import('./test/tokens')).makeAccessToken({ sub: 'aday-1', role: 'Candidate', name: 'Ayşe Demir' })
    mockFetch((url) => (String(url).endsWith('/api/auth/login') ? jsonResponse(200, { accessToken, refreshToken: 'yeni' }) : jsonResponse(401)))
    signInAs('Candidate')
    await renderApp('/profile')

    const { apiRequest } = await import('./api/client')
    await expect(apiRequest('/api/exams')).rejects.toThrow()

    expect(await screen.findByRole('heading', { name: 'Oturumunuzun süresi doldu' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: 'Tekrar giriş yap' }))
    await userEvent.type(screen.getByLabelText('E-posta'), 'aday@aiguard.local')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Sifre123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Profil' })).toBeInTheDocument()
  })

  it('tanımsız adreste "Sayfa bulunamadı" gösterir', async () => {
    await renderApp('/olmayan-sayfa')

    expect(screen.getByRole('heading', { name: 'Sayfa bulunamadı' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Giriş sayfasına git' })).toHaveAttribute('href', '/login')
  })
})
