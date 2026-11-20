import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from './auth/tokenStore'
import { jsonResponse, mockFetch } from './test/fetch'
import { renderApp, signInAs } from './test/renderApp'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('yönlendirme', () => {
  it('giriş yapmamış kullanıcıyı giriş sayfasına gönderir', () => {
    renderApp('/admin')

    expect(screen.getByRole('heading', { name: 'Giriş' })).toBeInTheDocument()
  })

  it.each([
    ['Admin', 'Yönetim'],
    ['Reviewer', 'İncelemeler'],
    ['Candidate', 'Sınavlarım'],
  ] as const)('%s kökten kendi ana sayfasına yönlenir', (role, heading) => {
    signInAs(role)
    renderApp('/')

    expect(screen.getByRole('heading', { name: heading })).toBeInTheDocument()
  })

  it('başka rolün sayfasına giren kullanıcıyı kendi ana sayfasına döndürür', () => {
    signInAs('Candidate')
    renderApp('/admin')

    expect(screen.getByRole('heading', { name: 'Sınavlarım' })).toBeInTheDocument()
  })

  it('menüde yalnızca kullanıcının rolüne ait bağlantılar görünür', () => {
    signInAs('Reviewer', 'Mert Yılmaz')
    renderApp('/review')

    const nav = screen.getByRole('navigation', { name: 'Ana menü' })
    expect(nav).toHaveTextContent('İncelemeler')
    expect(nav).toHaveTextContent('Profil')
    expect(nav).not.toHaveTextContent('Yönetim')
    expect(nav).not.toHaveTextContent('Sınavlarım')
    expect(screen.getByText('Mert Yılmaz · Değerlendirici')).toBeInTheDocument()
  })

  it('Çıkış butonu oturumu kapatıp giriş sayfasına götürür', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Admin')
    renderApp('/admin')

    await userEvent.click(screen.getByRole('button', { name: 'Çıkış' }))

    expect(await screen.findByRole('heading', { name: 'Giriş' })).toBeInTheDocument()
    expect(tokenStore.get()).toBeNull()
    expect(fetchMock.mock.calls[0][0]).toBe('/api/auth/logout')
  })

  it('tanımsız adreste "Sayfa bulunamadı" gösterir', () => {
    renderApp('/olmayan-sayfa')

    expect(screen.getByRole('heading', { name: 'Sayfa bulunamadı' })).toBeInTheDocument()
  })
})
