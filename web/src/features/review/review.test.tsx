import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { jsonResponse, mockFetch } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'

const examSummary = { id: 'ex1', name: 'Güz Genel Yetenek', type: 'GeneralAptitude', durationMinutes: 30, windowStartAt: '2026-11-20T08:00:00Z', windowEndAt: '2026-11-20T18:00:00Z', latestStartAt: '2026-11-20T17:30:00Z', retentionDays: 30, questionCount: 3, attemptUsed: null }

const row = (overrides: Record<string, unknown> = {}) => ({
  sessionId: 's1',
  candidateId: 'c1',
  candidateName: 'Elif Yılmaz',
  startedAt: '2026-11-20T09:00:00Z',
  endedAt: '2026-11-20T09:29:00Z',
  riskScore: 64,
  flags: ['Risky', 'NoAgent'],
  status: 'AwaitingReview',
  decision: null,
  topReason: 'Karede telefon görüldü (8 sn).',
  ...overrides,
})

const detail = (overrides: Record<string, unknown> = {}) => ({
  sessionId: 's1',
  examId: 'ex1',
  examName: 'Güz Genel Yetenek',
  candidateId: 'c1',
  candidateName: 'Elif Yılmaz',
  state: 'Completed',
  status: 'AwaitingReview',
  createdAt: '2026-11-20T08:58:00Z',
  startedAt: '2026-11-20T09:00:00Z',
  endedAt: '2026-11-20T09:29:00Z',
  riskScore: 64,
  flags: ['Risky'],
  decision: null,
  notes: [],
  events: [
    {
      id: 'ev1',
      type: 'PHONE',
      source: 'Camera',
      startedAt: '2026-11-20T09:06:00Z',
      durationSec: 8,
      confidence: 0.91,
      reason: 'Karede telefon görüldü (8 sn).',
      contribution: 22,
      isFalseAlarm: false,
      falseAlarmMarkedBy: null,
      falseAlarmMarkedAt: null,
      evidenceUrl: null,
    },
  ],
  scoreHistory: [{ at: '2026-11-20T09:06:00Z', score: 22 }],
  monitoringDataDeletedAt: null,
  ...overrides,
})

describe('değerlendirici ekranları', () => {
  it('panel bekleyen dosya sayısını ve gerekçeyi gösterir; filtre sunucuya gider', async () => {
    const fetchMock = mockFetch((url) => {
      if (url === '/api/exams/ex1') return jsonResponse(200, examSummary)
      if (url.startsWith('/api/review/exams/ex1/sessions')) return jsonResponse(200, { pendingCount: 1, sessions: [row()] })
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')

    const caseRow = await screen.findByRole('link', { name: /Elif Yılmaz/ })
    expect(within(caseRow).getByText('Karede telefon görüldü (8 sn).')).toBeInTheDocument()
    expect(within(caseRow).getByText('Ajansız')).toBeInTheDocument()
    expect(caseRow).toHaveAttribute('href', '/review/sessions/s1')

    await userEvent.click(screen.getByRole('button', { name: 'Riskli' }))
    await waitFor(() => expect(fetchMock.mock.calls.map(([url]) => url)).toContain('/api/review/exams/ex1/sessions?risky=true&sort=Priority'))
  })

  it('dosyada yanlış alarm işaretler ve dosyayı yeniler', async () => {
    let marked = false
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/events/ev1/false-alarm' && init.method === 'POST') {
        marked = true
        return jsonResponse(204)
      }
      if (url === '/api/review/sessions/s1') return jsonResponse(200, marked ? detail({ riskScore: 0, events: [{ ...detail().events[0], contribution: 0, isFalseAlarm: true, falseAlarmMarkedBy: 'Test Kullanıcı' }] }) : detail())
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    expect(await screen.findByRole('heading', { name: 'Elif Yılmaz' })).toBeInTheDocument()
    expect(screen.getByText('Tespit edilen olaylar kopya kanıtı değildir; karar size aittir.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Yanlış alarm olarak işaretle' }))

    expect(await screen.findByText(/Yanlış alarm · Test Kullanıcı/)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/events/ev1/false-alarm' && init?.method === 'POST')).toBe(true)
  })

  it('karar basılı tutmadan gönderilmez; basılı tutunca seçilen kararla gider', async () => {
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/review/sessions/s1/decision' && init.method === 'POST') return jsonResponse(204)
      if (url === '/api/review/sessions/s1') return jsonResponse(200, detail())
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    await userEvent.click(await screen.findByRole('radio', { name: 'Şüpheli' }))
    const confirm = screen.getByRole('button', { name: /Şüpheli: basılı tut ve onayla/ })

    // Kısa basış: gönderilmez.
    fireEvent.pointerDown(confirm)
    fireEvent.pointerUp(confirm)
    await new Promise((resolve) => setTimeout(resolve, 1_100))
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/review/sessions/s1/decision')).toBe(false)

    // 1 sn basılı tutma: gönderilir.
    fireEvent.pointerDown(confirm)
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => url === '/api/review/sessions/s1/decision')).toBe(true), { timeout: 2_000 })
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/review/sessions/s1/decision')
    expect(JSON.parse(call![1]!.body as string)).toEqual({ decision: 'Suspicious', note: null })
  })

  it('yönetici değerlendirici dosya adresine giderse kendi ana sayfasına döner', async () => {
    mockFetch((url) => (url === '/api/reports/exams/ex1/sessions/s1' ? jsonResponse(404) : jsonResponse(404)))
    signInAs('Admin')
    await renderApp('/review/sessions/s1')
    expect(await screen.findByRole('heading', { level: 1, name: /Merhaba, Test/ })).toBeInTheDocument()
  })
})
