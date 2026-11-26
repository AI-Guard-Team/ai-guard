import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { jsonResponse, mockFetch } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'
import { windowPhase } from './examWindow'

const hour = 3_600_000
const exam = (overrides: Record<string, unknown> = {}) => ({
  id: 'e1',
  name: 'Güz Genel Yetenek',
  type: 'GeneralAptitude',
  durationMinutes: 60,
  windowStartAt: new Date(Date.now() - hour).toISOString(),
  windowEndAt: new Date(Date.now() + 5 * hour).toISOString(),
  latestStartAt: new Date(Date.now() + 4 * hour).toISOString(),
  retentionDays: 30,
  questionCount: 0,
  attemptUsed: null,
  ...overrides,
})

describe('sınav penceresi evresi', () => {
  const now = Date.parse('2026-11-26T12:00:00Z')
  const window = (start: string, end: string, latest: string) => ({ windowStartAt: start, windowEndAt: end, latestStartAt: latest })

  it('pencereye göre planlandı, açık, son başlangıç geçti ve sona erdi döner', () => {
    expect(windowPhase(window('2026-11-27T10:00:00Z', '2026-11-27T18:00:00Z', '2026-11-27T17:00:00Z'), now)).toBe('upcoming')
    expect(windowPhase(window('2026-11-26T10:00:00Z', '2026-11-26T18:00:00Z', '2026-11-26T17:00:00Z'), now)).toBe('open')
    expect(windowPhase(window('2026-11-26T10:00:00Z', '2026-11-26T12:30:00Z', '2026-11-26T11:30:00Z'), now)).toBe('closing')
    expect(windowPhase(window('2026-11-25T10:00:00Z', '2026-11-25T18:00:00Z', '2026-11-25T17:00:00Z'), now)).toBe('closed')
  })
})

describe('yönetici ekranları', () => {
  it('sınav listesi durumu ve sorusuz sınavı gösterir, satır düzenleyiciye gider', async () => {
    mockFetch((url) => (url === '/api/exams' ? jsonResponse(200, [exam()]) : jsonResponse(200, [])))
    signInAs('Admin')
    await renderApp('/admin/exams')

    const row = await screen.findByRole('link', { name: /Güz Genel Yetenek/ })
    expect(within(row).getByText('Erişime açık')).toBeInTheDocument()
    expect(within(row).getByText('Soru yok')).toBeInTheDocument()
    expect(row).toHaveAttribute('href', '/admin/exams/e1')
  })

  it('yeni sınavda yalnızca genel bilgiler açık; kaydedince sorulara geçer', async () => {
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/exams' && init.method === 'POST') return jsonResponse(201, { id: 'new1' })
      if (url === '/api/exams/new1') return jsonResponse(200, exam({ id: 'new1', name: 'Yeni sınav' }))
      return jsonResponse(200, [])
    })
    signInAs('Admin')
    await renderApp('/admin/exams/new')

    expect(screen.getByText('2. Sorular').closest('[aria-disabled="true"]')).not.toBeNull()
    await userEvent.type(screen.getByLabelText('Sınav adı'), 'Yeni sınav')
    await userEvent.click(screen.getByRole('button', { name: 'Kaydet ve devam et' }))

    await screen.findByRole('heading', { name: 'Sorular' })
    const post = fetchMock.mock.calls.find(([url, init]) => url === '/api/exams' && init?.method === 'POST')
    expect(JSON.parse(post![1]!.body as string)).toMatchObject({ name: 'Yeni sınav', type: 'GeneralAptitude', durationMinutes: 60, retentionDays: 30 })
  })

  it('davet formu boş alanları sunucuya göndermeden söyler, sonra davet gönderir', async () => {
    const fetchMock = mockFetch((url, init) => (url === '/api/users' && init.method === 'POST' ? jsonResponse(201, { id: 'u9' }) : jsonResponse(200, [])))
    signInAs('Admin')
    await renderApp('/admin/users')

    await userEvent.click(await screen.findByRole('button', { name: 'Kullanıcı davet et' }))
    await userEvent.click(screen.getByRole('button', { name: 'Davet gönder' }))
    expect(screen.getByText('Ad soyad zorunludur.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)

    await userEvent.type(screen.getByLabelText('Ad soyad'), 'Ayşe Demir')
    await userEvent.type(screen.getByLabelText('E-posta'), 'ayse@kurum.edu.tr')
    await userEvent.click(screen.getByRole('radio', { name: /Değerlendirici/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Davet gönder' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/users' && init?.method === 'POST')).toBe(true))
    const post = fetchMock.mock.calls.find(([url, init]) => url === '/api/users' && init?.method === 'POST')
    expect(JSON.parse(post![1]!.body as string)).toEqual({ fullName: 'Ayşe Demir', email: 'ayse@kurum.edu.tr', role: 'Reviewer' })
  })

  const riskMocks = (locked: boolean) =>
    mockFetch((url, init) => {
      if (url === '/api/risk-settings')
        return jsonResponse(200, { sessionThreshold: 10, repetitionStep: 0.25, maxRepetitionMultiplier: 2, durationScaleSeconds: 30, events: [] })
      if (url === '/api/exams/e1/risk-settings' && (init.method ?? 'GET') === 'GET')
        return jsonResponse(200, {
          examId: 'e1',
          sessionThreshold: 10,
          defaultSessionThreshold: 10,
          locked,
          events: [
            { type: 'PHONE', weight: 5, decayPerMinute: 0.02, threshold: null, defaultWeight: 5, defaultDecayPerMinute: 0.02, defaultThreshold: null, customized: false },
            { type: 'TAB_BLUR', weight: 1, decayPerMinute: 0.05, threshold: null, defaultWeight: 1, defaultDecayPerMinute: 0.05, defaultThreshold: null, customized: false },
          ],
        })
      if (url === '/api/exams/e1/risk-settings') return new Response(null, { status: 204 })
      if (url === '/api/exams/e1') return jsonResponse(200, exam({ questionCount: 3 }))
      return jsonResponse(200, [])
    })

  it('sınava özel risk ağırlığını düzenler; yalnızca değişen değer gider', async () => {
    const fetchMock = riskMocks(false)
    signInAs('Admin')
    await renderApp('/admin/exams/e1/risk')

    const weight = await screen.findByLabelText('Telefon: ağırlık')
    expect(screen.getAllByText('Varsayılan 5,0').length).toBeGreaterThan(0)
    await userEvent.clear(weight)
    await userEvent.type(weight, '12,5')
    expect(screen.getByText('Bu sınava özel')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Kaydet' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/exams/e1/risk-settings' && init?.method === 'PUT')).toBe(true))
    const put = fetchMock.mock.calls.find(([url, init]) => url === '/api/exams/e1/risk-settings' && init?.method === 'PUT')
    expect(JSON.parse(put![1]!.body as string)).toEqual({ sessionThreshold: null, events: [{ type: 'PHONE', weight: 12.5, decayPerMinute: 0.02, threshold: null }] })
  })

  it('oturum açılmış sınavda risk ayarları kilitli', async () => {
    riskMocks(true)
    signInAs('Admin')
    await renderApp('/admin/exams/e1/risk')

    expect(await screen.findByLabelText('Telefon: ağırlık')).toBeDisabled()
    expect(screen.getByText(/ayarlar kilitli/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kaydet' })).not.toBeInTheDocument()
  })
})
