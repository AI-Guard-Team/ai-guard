import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { jsonResponse, mockFetch } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'

const hour = 3_600_000
const exam = {
  id: 'e1',
  name: 'Güz Genel Yetenek',
  type: 'GeneralAptitude',
  durationMinutes: 60,
  windowStartAt: new Date(Date.now() + 26 * hour).toISOString(),
  windowEndAt: new Date(Date.now() + 30 * hour).toISOString(),
  latestStartAt: new Date(Date.now() + 29 * hour).toISOString(),
  retentionDays: 30,
  questionCount: 12,
  attemptUsed: false,
  mySession: null,
}

afterEach(() => localStorage.clear())

describe('genel bakış (K-42)', () => {
  it('aday sıradaki sınavını, açılışa kalan süreyi ve eksik kayıt fotoğrafını görür', async () => {
    mockFetch((url) => (url === '/api/exams' ? jsonResponse(200, [exam]) : url === '/api/me/biometric' ? jsonResponse(200, { enrolled: false }) : jsonResponse(404)))
    signInAs('Candidate', 'Ayşe Demir')
    await renderApp('/overview')

    expect(screen.getByRole('heading', { level: 1, name: 'Merhaba, Ayşe 👋' })).toBeInTheDocument()
    const next = await screen.findByRole('region', { name: 'Sıradaki sınavınız' })
    expect(within(next).getByText('Güz Genel Yetenek')).toBeInTheDocument()
    expect(within(next).getByText(/1 gün \d+ saat sonra açılıyor/)).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Hazırlık listesi' })).getByRole('link', { name: /Yükle/ })).toHaveAttribute('href', '/profile/photo')
  })

  it('yan menü daraltılır ve tercih hatırlanır', async () => {
    mockFetch(() => jsonResponse(404))
    signInAs('Reviewer')
    await renderApp('/overview')

    await userEvent.click(screen.getByRole('button', { name: 'Menüyü daralt' }))
    expect(document.querySelector('.app-shell')).toHaveAttribute('data-collapsed', 'true')
    expect(localStorage.getItem('aiguard.sidebar.collapsed')).toBe('1')
  })
})
