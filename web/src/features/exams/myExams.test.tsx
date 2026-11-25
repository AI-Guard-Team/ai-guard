import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { ExamSummary } from '../../api/exams'
import { withCandidateBasics } from '../../test/candidateServer'
import { jsonResponse, mockFetch, type FetchHandler } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'

const NOW = new Date('2026-11-25T12:00:00Z')

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

function exam(id: string, overrides: Partial<ExamSummary> = {}): ExamSummary {
  return {
    id,
    name: `Sınav ${id}`,
    type: 'GeneralAptitude',
    durationMinutes: 60,
    windowStartAt: '2026-11-25T09:00:00Z',
    windowEndAt: '2026-11-25T18:00:00Z',
    latestStartAt: '2026-11-25T17:00:00Z',
    retentionDays: 30,
    questionCount: 12,
    attemptUsed: false,
    mySession: null,
    ...overrides,
  }
}

async function open(exams: ExamSummary[], next: FetchHandler = () => jsonResponse(500), enrolled = true) {
  const fetchMock = mockFetch(withCandidateBasics((url, init) => (url === '/api/exams' ? jsonResponse(200, exams) : next(url, init)), { enrolled }))
  signInAs('Candidate')
  await renderApp('/exams')
  return fetchMock
}

const card = (name: string) => screen.getByRole('article', { name })

describe('Sınavlarım', () => {
  it('her sınavı durumuna göre gösterir', async () => {
    await open([
      exam('a'),
      exam('b', { windowStartAt: '2026-11-26T09:00:00Z' }),
      exam('c', { attemptUsed: true, mySession: { id: 's-c', state: 'Completed', endedAt: '2026-11-25T11:00:00Z' } }),
      exam('d', { mySession: { id: 's-d', state: 'InProgress' } }),
    ])

    expect(await screen.findByRole('heading', { level: 1, name: 'Sınavlarım' })).toBeInTheDocument()
    expect(within(card('Sınav a')).getByRole('button', { name: 'Sınav öncesi adımlara geç' })).toBeEnabled()
    expect(within(card('Sınav b')).getByText('Yaklaşan')).toBeInTheDocument()
    expect(within(card('Sınav b')).queryByRole('button')).not.toBeInTheDocument()
    expect(within(card('Sınav c')).getByText(/Teslim edildi/)).toBeInTheDocument()
    expect(within(card('Sınav d')).getByRole('link', { name: 'Sınava dön' })).toHaveAttribute('href', '/exams/s-d')
  })

  it('kayıt fotoğrafı yoksa uyarır ve sınav yerine fotoğraf yüklemeye yönlendirir', async () => {
    await open([exam('a')], undefined, false)

    expect(await screen.findByText('Kayıt fotoğrafınızı yükleyin')).toBeInTheDocument()
    expect(within(card('Sınav a')).getByRole('link', { name: 'Kayıt fotoğrafı yükle' })).toHaveAttribute('href', '/profile/photo')
    expect(within(card('Sınav a')).queryByRole('button', { name: 'Sınav öncesi adımlara geç' })).not.toBeInTheDocument()
  })

  it('başlatınca oturumu açıp sihirbaza gider', async () => {
    const fetchMock = await open([exam('a')], (url, init) =>
      url === '/api/exams/a/sessions' && init.method === 'POST' ? jsonResponse(201, { sessionId: 'yeni', state: 'ConsentPending' }) : jsonResponse(500),
    )

    await userEvent.click(within(card('Sınav a')).getByRole('button', { name: 'Sınav öncesi adımlara geç' }))

    expect(await screen.findByText('Oturum açılamadı')).toBeInTheDocument() // sihirbaz sayfası açıldı (oturum ucu bu testte yok)
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/exams/a/sessions')).toBe(true)
  })

  it('sunucu başlatmayı reddederse nedenini kartta gösterir', async () => {
    await open([exam('a')], (url) =>
      url === '/api/exams/a/sessions' ? jsonResponse(409, { title: 'Sınav başlatılamıyor.', detail: 'Bu sınav için oturum hakkınızı kullandınız.' }) : jsonResponse(500),
    )

    await userEvent.click(within(card('Sınav a')).getByRole('button', { name: 'Sınav öncesi adımlara geç' }))

    expect(await within(card('Sınav a')).findByRole('alert')).toHaveTextContent('oturum hakkınızı kullandınız')
  })

  it('atanmış sınav yoksa boş durum gösterir', async () => {
    await open([])

    expect(await screen.findByRole('heading', { name: 'Size atanmış sınav yok' })).toBeInTheDocument()
  })

  it('liste yüklenemezse yeniden denenebilir', async () => {
    let calls = 0
    mockFetch(
      withCandidateBasics((url) => {
        if (url !== '/api/exams') return jsonResponse(500)
        calls += 1
        return calls === 1 ? jsonResponse(500) : jsonResponse(200, [exam('a')])
      }),
    )
    signInAs('Candidate')
    await renderApp('/exams')

    await userEvent.click(await screen.findByRole('button', { name: 'Yeniden dene' }))

    expect(await screen.findByRole('article', { name: 'Sınav a' })).toBeInTheDocument()
  })
})
