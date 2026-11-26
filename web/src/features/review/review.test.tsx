import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { jsonResponse, mockFetch } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'

// SignalR taklidi S5'teki testlerden (Şeyma, K-34): bağlantı açılıp kapanması ve gelen mesaj sınanıyor.
type ReadyHandler = (message: unknown) => void
const hub = vi.hoisted(() => ({ handlers: new Map<string, (message: unknown) => void>(), started: 0, stopped: 0 }))

vi.mock('@microsoft/signalr', () => ({
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return {
        on: (name: string, handler: ReadyHandler) => hub.handlers.set(name, handler),
        start: () => {
          hub.started += 1
          return Promise.resolve()
        },
        stop: () => {
          hub.stopped += 1
          return Promise.resolve()
        },
      }
    }
  },
}))

beforeEach(() => {
  hub.handlers.clear()
  hub.started = 0
  hub.stopped = 0
})

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

const phoneEvent = {
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
  evidenceUrl: '/api/evidence/f1?exp=1&sig=x',
}

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
  events: [phoneEvent],
  scoreHistory: [{ at: '2026-11-20T09:06:00Z', score: 22 }],
  monitoringDataDeletedAt: null,
  ...overrides,
})

const listServer = (list: () => unknown) => (url: string) => {
  if (url === '/api/exams/ex1') return jsonResponse(200, examSummary)
  if (url.startsWith('/api/review/exams/ex1/sessions')) return jsonResponse(200, list())
  return jsonResponse(404)
}

const reviewCalls = (fetchMock: ReturnType<typeof mockFetch>) => fetchMock.mock.calls.filter(([url]) => String(url).startsWith('/api/review/exams/ex1/sessions'))

describe('inceleme paneli (sınavın dosyaları)', () => {
  it('bekleyen dosya sayısını, işaret rozetlerini ve gerekçeyi gösterir', async () => {
    mockFetch(listServer(() => ({ pendingCount: 1, sessions: [row()] })))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')

    const caseRow = await screen.findByRole('link', { name: /Elif Yılmaz/ })
    expect(within(caseRow).getByText('Karede telefon görüldü (8 sn).')).toBeInTheDocument()
    expect(within(caseRow).getByText('Ajansız')).toBeInTheDocument()
    expect(caseRow).toHaveAttribute('href', '/review/sessions/s1')
    expect(screen.getByText('dosya kararınızı bekliyor')).toBeInTheDocument()
  })

  it('süzgeç ve sıralamayı sunucu sorgusuna yazar', async () => {
    const fetchMock = mockFetch(listServer(() => ({ pendingCount: 1, sessions: [row()] })))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')
    await screen.findByRole('link', { name: /Elif Yılmaz/ })

    await userEvent.click(screen.getByRole('button', { name: 'Riskli' }))
    await waitFor(() => expect(reviewCalls(fetchMock).map(([url]) => url)).toContain('/api/review/exams/ex1/sessions?risky=true&sort=Priority'))
    await userEvent.selectOptions(screen.getByRole('combobox'), 'Score')
    await waitFor(() => expect(reviewCalls(fetchMock).map(([url]) => url)).toContain('/api/review/exams/ex1/sessions?risky=true&sort=Score'))
  })

  it('kendi sınavına yeni dosya düşünce listeyi yeniler ve haber verir', async () => {
    const fetchMock = mockFetch(listServer(() => ({ pendingCount: 1, sessions: [row()] })))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')
    await screen.findByRole('link', { name: /Elif Yılmaz/ })
    await waitFor(() => expect(hub.started).toBe(1))
    const before = reviewCalls(fetchMock).length

    act(() => hub.handlers.get('SessionReadyForReview')?.({ sessionId: 's2', examId: 'ex1', riskScore: 40, flags: ['Risky'], endedAt: '2026-11-20T10:00:00Z' }))

    expect(await screen.findByText('Yeni bir dosya incelemeye düştü.')).toBeInTheDocument()
    await waitFor(() => expect(reviewCalls(fetchMock).length).toBe(before + 1))
  })

  it('başka sınavın bildirimini yok sayar', async () => {
    const fetchMock = mockFetch(listServer(() => ({ pendingCount: 1, sessions: [row()] })))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')
    await screen.findByRole('link', { name: /Elif Yılmaz/ })
    await waitFor(() => expect(hub.started).toBe(1))
    const before = reviewCalls(fetchMock).length

    act(() => hub.handlers.get('SessionReadyForReview')?.({ sessionId: 's9', examId: 'baska', riskScore: 40, flags: [], endedAt: '2026-11-20T10:00:00Z' }))

    expect(screen.queryByText('Yeni bir dosya incelemeye düştü.')).not.toBeInTheDocument()
    expect(reviewCalls(fetchMock).length).toBe(before)
  })

  it('sayfadan çıkınca bildirim bağlantısını kapatır', async () => {
    mockFetch(listServer(() => ({ pendingCount: 0, sessions: [] })))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')
    await waitFor(() => expect(hub.started).toBe(1))

    cleanup()
    await waitFor(() => expect(hub.stopped).toBe(1))
  })

  it('atanmadığı sınavda sunucunun mesajını gösterir', async () => {
    mockFetch((url) => (url.startsWith('/api/review') ? jsonResponse(403, { title: 'Erişim yok.', detail: 'Bu sınava değerlendirici olarak atanmadınız.' }) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review/exams/ex1')

    expect(await screen.findByText(/Bu sınava değerlendirici olarak atanmadınız/)).toBeInTheDocument()
  })
})

describe('dosya incelemesi', () => {
  it('olay ayrıntısını, kanıtı (bulanık) ve skor gerekçesini gösterir', async () => {
    mockFetch((url) => (url === '/api/review/sessions/s1' ? jsonResponse(200, detail()) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    expect(await screen.findByRole('heading', { name: 'Elif Yılmaz' })).toBeInTheDocument()
    expect(screen.getByText('Tespit edilen olaylar kopya kanıtı değildir; karar size aittir.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Olay ayrıntısı: Telefon' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Telefon: skora 22,0 puan/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Kanıtı göster' })).toBeInTheDocument()
  })

  it('yanlış alarmı onay penceresinden sonra işaretler; vazgeçilirse istek gitmez', async () => {
    let marked = false
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/events/ev1/false-alarm' && init.method === 'POST') {
        marked = true
        return jsonResponse(204)
      }
      if (url === '/api/review/sessions/s1') return jsonResponse(200, marked ? detail({ riskScore: 0, events: [{ ...phoneEvent, contribution: 0, isFalseAlarm: true, falseAlarmMarkedBy: 'Test Kullanıcı' }] }) : detail())
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')
    const markCalls = () => fetchMock.mock.calls.filter(([url]) => url === '/api/events/ev1/false-alarm')

    await userEvent.click(await screen.findByRole('button', { name: 'Yanlış alarm olarak işaretle' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Vazgeç' }))
    expect(markCalls()).toHaveLength(0)

    await userEvent.click(screen.getByRole('button', { name: 'Yanlış alarm olarak işaretle' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'İşaretle' }))

    expect(await screen.findByText(/Yanlış alarm · Test Kullanıcı/)).toBeInTheDocument()
    expect(markCalls()).toHaveLength(1)
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

  it('sunucu çakışma bildirirse nedenini gösterir', async () => {
    mockFetch((url, init) => {
      if (url === '/api/review/sessions/s1/decision' && init.method === 'POST') return jsonResponse(409, { title: 'İşlem yapılamıyor.', detail: 'Bu oturum için zaten karar verilmiş.' })
      if (url === '/api/review/sessions/s1') return jsonResponse(200, detail())
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    await userEvent.click(await screen.findByRole('radio', { name: 'Temiz' }))
    fireEvent.pointerDown(screen.getByRole('button', { name: /Temiz: basılı tut ve onayla/ }))
    expect(await screen.findByText('Bu oturum için zaten karar verilmiş.', {}, { timeout: 2_500 })).toBeInTheDocument()
  })

  it('karar gerekmeyen oturumda karar bölümü çıkmaz', async () => {
    mockFetch((url) => (url === '/api/review/sessions/s1' ? jsonResponse(200, detail({ status: 'Completed', flags: [] })) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    await screen.findByRole('heading', { name: 'Elif Yılmaz' })
    expect(screen.queryByRole('heading', { name: 'Dosyayı karara bağlayın' })).not.toBeInTheDocument()
  })

  it('karar verilmiş oturumda kararı gösterir; karar ve yanlış alarm düğmesi çıkmaz', async () => {
    const decision = { decision: 'Violation', note: 'Telefon açıkça görülüyor.', reviewerName: 'Mert Yılmaz', decidedAt: '2026-11-20T11:00:00Z' }
    mockFetch((url) => (url === '/api/review/sessions/s1' ? jsonResponse(200, detail({ status: 'Decided', decision })) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    expect(await screen.findByRole('img', { name: 'Karar: İhlal' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Dosyayı karara bağlayın' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yanlış alarm olarak işaretle' })).not.toBeInTheDocument()
  })

  it('not ekler ve dosyayı yeniler', async () => {
    let notes: unknown[] = []
    const fetchMock = mockFetch((url, init) => {
      if (url === '/api/review/sessions/s1/notes' && init.method === 'POST') {
        notes = [{ id: 'n1', text: 'Kanıtı tekrar izledim.', authorName: 'Test Kullanıcı', createdAt: '2026-11-20T10:00:00Z' }]
        return jsonResponse(204)
      }
      if (url === '/api/review/sessions/s1') return jsonResponse(200, detail({ notes }))
      return jsonResponse(404)
    })
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    await userEvent.type(await screen.findByLabelText('Not ekle'), 'Kanıtı tekrar izledim.')
    await userEvent.click(screen.getByRole('button', { name: 'Notu kaydet' }))

    expect(await screen.findByText('Kanıtı tekrar izledim.', { selector: '.notes-text' })).toBeInTheDocument()
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/review/sessions/s1/notes')
    expect(JSON.parse(call![1]!.body as string)).toEqual({ text: 'Kanıtı tekrar izledim.' })
  })

  it('saklama süresi dolmuşsa sinyal kaydı yerine açıklama gösterir', async () => {
    mockFetch((url) => (url === '/api/review/sessions/s1' ? jsonResponse(200, detail({ events: [], scoreHistory: [], monitoringDataDeletedAt: '2026-12-20T03:00:00Z' })) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review/sessions/s1')

    expect(await screen.findByText('İzleme verileri silindi')).toBeInTheDocument()
    expect(screen.queryByRole('slider', { name: 'Zaman çizgisi' })).not.toBeInTheDocument()
  })
})

describe('değerlendiricinin sınavları', () => {
  it('atanan sınavları bekleyen sayısıyla bağlantı olarak listeler', async () => {
    mockFetch((url) => (url === '/api/exams' ? jsonResponse(200, [examSummary]) : url.startsWith('/api/review/exams/ex1/sessions') ? jsonResponse(200, { pendingCount: 2, sessions: [row(), row({ sessionId: 's2' })] }) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review')

    const link = await screen.findByRole('link', { name: /Güz Genel Yetenek/ })
    expect(link).toHaveAttribute('href', '/review/exams/ex1')
    expect(screen.getByText('2 dosya kararınızı bekliyor.')).toBeInTheDocument()
  })

  it('atanmış sınav yoksa bunu söyler', async () => {
    mockFetch((url) => (url === '/api/exams' ? jsonResponse(200, []) : jsonResponse(404)))
    signInAs('Reviewer')
    await renderApp('/review')

    expect(await screen.findByText('Atanmış sınav yok')).toBeInTheDocument()
  })

  it('aday değerlendirici sayfalarına giremez, kendi ana sayfasına döner', async () => {
    mockFetch(() => jsonResponse(404))
    signInAs('Candidate')
    await renderApp('/review/sessions/s1')
    expect(await screen.findByRole('heading', { level: 1, name: /Merhaba, Test/ })).toBeInTheDocument()
  })
})
