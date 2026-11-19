import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp, signInAs } from './test/renderApp'

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

  it('tanımsız adreste "Sayfa bulunamadı" gösterir', () => {
    renderApp('/olmayan-sayfa')

    expect(screen.getByRole('heading', { name: 'Sayfa bulunamadı' })).toBeInTheDocument()
  })
})
