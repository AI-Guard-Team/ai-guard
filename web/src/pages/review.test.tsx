import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { jsonResponse, mockFetch, type FetchHandler } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'

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

// Recharts jsdom'da ölçü alamaz; grafik kendi başına test edilmiyor, yalnızca erişilebilir adı kontrol ediliyor.
vi.mock('recharts', () => {
  const Empty = () => null
  return {
    ResponsiveContainer: ({ children }: { children?: unknown }) => <div data-testid="chart">{children as never}</div>,
    LineChart: Empty,
    Line: Empty,
    XAxis: Empty,
    YAxis: Empty,
    CartesianGrid: Empty,
    Tooltip: Empty,
  }
})

const EXAM = '22222222-2222-2222-2222-222222222222'
const SESSION = '33333333-3333-3333-3333-333333333333'
const EVENT = '44444444-4444-4444-4444-444444444444'

beforeEach(() => {
  hub.handlers.clear()
  hub.started = 0
  hub.stopped = 0
})

afterEach(() => {
  vi.unstubAllGlobals()
})

function row(overrides: Record<string, unknown> = {}) {
  return {
    sessionId: SESSION,
    candidateId: 'aday-1',
    candidateName: 'Ayşe Aday',
    startedAt: '2026-11-25T10:00:00Z',
    endedAt: '2026-11-25T11:00:00Z',
    riskScore: 42.5,
    flags: ['Risky', 'NoAgent'],
    status: 'AwaitingReview',
    decision: null,
    topReason: 'Birden fazla yüz algılandı',
    ...overrides,
  }
}

function detail(overrides: Record<string, unknown> = {}) {
  return {
    sessionId: SESSION,
    examId: EXAM,
    examName: 'Genel Yetenek',
    candidateId: 'aday-1',
    candidateName: 'Ayşe Aday',
    state: 'Completed',
    status: 'AwaitingReview',
    createdAt: '2026-11-25T09:50:00Z',
    startedAt: '2026-11-25T10:00:00Z',
    endedAt: '2026-11-25T11:00:00Z',
    riskScore: 42.5,
    flags: ['Risky'],
    decision: null,
    notes: [],
    events: [
      {
        id: EVENT,
        type: 'MULTIPLE_FACES',
        source: 'Camera',
        startedAt: '2026-11-25T10:20:00Z',
        durationSec: 12,
        confidence: 0.9,
        reason: 'Kadrajda iki yüz var',
        contribution: 20,
        isFalseAlarm: false,
        evidenceUrl: '/api/evidence/abc?exp=1&sig=x',
      },
    ],
    scoreHistory: [
      { at: '2026-11-25T10:00:00Z', score: 0 },
      { at: '2026-11-25T10:20:00Z', score: 42.5 },
    ],
    monitoringDataDeletedAt: null,
    ...overrides,
  }
}

describe('oturum listesi', () => {
  function renderList(handler?: FetchHandler) {
    const fetchMock = mockFetch(
      handler ??
        ((url) => {
          if (url.endsWith(`/api/review/sessions/${SESSION}`)) return jsonResponse(200, detail())
          if (url.includes('/sessions')) return jsonResponse(200, { pendingCount: 3, sessions: [row()] })
          return jsonResponse(200, { id: EXAM, name: 'Genel Yetenek' })
        }),
    )
    signInAs('Reviewer')
    renderApp(`/review/exams/${EXAM}`)
    return fetchMock
  }

  it('karar bekleyen sayısını, işaret rozetlerini ve gerekçeyi gösterir', async () => {
    renderList()

    expect(await screen.findByRole('link', { name: 'Ayşe Aday' })).toHaveAttribute('href', `/review/sessions/${SESSION}`)
    expect(screen.getByText('oturum karar bekliyor').parentElement).toHaveTextContent('3 oturum karar bekliyor')
    const table = within(screen.getByRole('table'))
    expect(table.getByText('Riskli')).toBeInTheDocument()
    expect(table.getByText('Ajansız')).toBeInTheDocument()
    expect(table.getByText('Karar bekliyor')).toBeInTheDocument()
    expect(table.getByText('Birden fazla yüz algılandı')).toBeInTheDocument()
    expect(table.getByText('42.5 · Riskli')).toBeInTheDocument()
  })

  it('süzgeç ve sıralamayı sorguya yazar', async () => {
    const fetchMock = renderList()
    await screen.findByRole('link', { name: 'Ayşe Aday' })

    await userEvent.selectOptions(screen.getByLabelText('Risk'), 'risky')
    await userEvent.click(screen.getByLabelText('Ajansız'))
    await userEvent.selectOptions(screen.getByLabelText('Sıralama'), 'Score')

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map(([url]) => String(url))
      expect(urls.some((url) => url.includes('risky=true') && url.includes('flags=NoAgent') && url.includes('sort=Score'))).toBe(true)
    })
  })

  it('yeni oturum bildirimi gelince listeyi yeniler ve haber verir', async () => {
    const fetchMock = renderList()
    await screen.findByRole('link', { name: 'Ayşe Aday' })
    const before = fetchMock.mock.calls.filter(([url]) => String(url).includes('/sessions')).length

    hub.handlers.get('SessionReadyForReview')?.({ sessionId: 'x', examId: EXAM, riskScore: 50, flags: ['Risky'] })

    expect(await screen.findByText('İncelemeye yeni bir oturum düştü.')).toBeInTheDocument()
    await waitFor(() => {
      expect(fetchMock.mock.calls.filter(([url]) => String(url).includes('/sessions')).length).toBeGreaterThan(before)
    })
  })

  it('başka sınavın bildirimini yok sayar', async () => {
    renderList()
    await screen.findByRole('link', { name: 'Ayşe Aday' })

    hub.handlers.get('SessionReadyForReview')?.({ sessionId: 'x', examId: '99999999-9999-9999-9999-999999999999', riskScore: 50, flags: [] })

    expect(screen.queryByText('İncelemeye yeni bir oturum düştü.')).not.toBeInTheDocument()
  })

  it('sayfadan çıkınca bildirim bağlantısını kapatır', async () => {
    renderList()
    const link = await screen.findByRole('link', { name: 'Ayşe Aday' })
    expect(hub.started).toBe(1)

    await userEvent.click(link)
    await waitFor(() => expect(hub.stopped).toBeGreaterThanOrEqual(1))
  })

  it('atanmadığı sınavda sunucu mesajını gösterir', async () => {
    renderList(() => jsonResponse(404))

    expect(await screen.findByRole('alert')).toHaveTextContent('İstenen kayıt bulunamadı.')
  })
})

describe('oturum incelemesi', () => {
  function renderDetail(data = detail(), extra?: (url: string, init: RequestInit) => Response | undefined) {
    const fetchMock = mockFetch((url, init) => {
      const custom = extra?.(url, init)
      if (custom) return custom
      if (url.endsWith(`/api/review/sessions/${SESSION}`)) return jsonResponse(200, data)
      return jsonResponse(204)
    })
    signInAs('Reviewer')
    renderApp(`/review/sessions/${SESSION}`)
    return fetchMock
  }

  it('olay, kanıt karesi ve grafik bilgisini gösterir', async () => {
    renderDetail()

    expect(await screen.findByRole('heading', { name: 'Ayşe Aday' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Aday raporu' })).toHaveAttribute('href', `/review/exams/${EXAM}/sessions/${SESSION}/report`)
    expect(screen.getAllByText('Birden fazla yüz').length).toBeGreaterThan(0)
    expect(screen.getByText('Kadrajda iki yüz var')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /Birden fazla yüz kanıt karesi/ })).toHaveAttribute('src', '/api/evidence/abc?exp=1&sig=x')
    expect(screen.getByRole('img', { name: /Risk skoru grafiği: 2 nokta, son skor 42.5/ })).toBeInTheDocument()
  })

  it('kararı onay penceresinden sonra gönderir', async () => {
    const fetchMock = renderDetail()
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    await userEvent.type(screen.getByLabelText('Karar notu (isteğe bağlı)'), 'Kanıt yeterli')
    await userEvent.click(screen.getByRole('button', { name: 'Şüpheli' }))

    const dialog = await screen.findByRole('dialog')
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/decision'))).toBe(false)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Onayla' }))

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith(`/api/review/sessions/${SESSION}/decision`))
      expect(call).toBeDefined()
      expect(JSON.parse(String(call?.[1]?.body))).toEqual({ decision: 'Suspicious', note: 'Kanıt yeterli' })
    })
    expect(await screen.findByText('Karar kaydedildi.')).toBeInTheDocument()
  })

  it('karar onayında vazgeçilirse istek atılmaz', async () => {
    const fetchMock = renderDetail()
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    await userEvent.click(screen.getByRole('button', { name: 'İhlal' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Vazgeç' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/decision'))).toBe(false)
  })

  it('karar gerekmeyen oturumda karar düğmesi göstermez', async () => {
    renderDetail(detail({ status: 'Completed', flags: [] }))
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    expect(screen.queryByRole('button', { name: 'Temiz' })).not.toBeInTheDocument()
    expect(screen.getByText('Bu oturum için karar gerekmiyor.')).toBeInTheDocument()
  })

  it('karar verilmiş oturumda kararı gösterir, karar ve yanlış alarm düğmesi çıkmaz', async () => {
    renderDetail(
      detail({
        status: 'Decided',
        decision: { decision: 'Violation', note: 'Telefon görüldü', reviewerName: 'Değerlendirici', decidedAt: '2026-11-25T12:00:00Z' },
      }),
    )
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    expect(screen.getByText('Telefon görüldü')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Yanlış alarm' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Temiz' })).not.toBeInTheDocument()
  })

  it('yanlış alarmı onaydan sonra işaretler', async () => {
    const fetchMock = renderDetail()
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    await userEvent.click(screen.getByRole('button', { name: 'Yanlış alarm' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }))

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith(`/api/events/${EVENT}/false-alarm`))
      expect(call?.[1]?.method).toBe('POST')
    })
  })

  it('not ekler', async () => {
    const fetchMock = renderDetail(detail(), (url) =>
      url.endsWith('/notes') ? jsonResponse(201, { id: 'n1', text: 'Not', authorName: 'Ben', createdAt: '2026-11-25T12:00:00Z' }) : undefined,
    )
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    await userEvent.type(screen.getByLabelText('Yeni not'), 'Dikkat edilmeli')
    await userEvent.click(screen.getByRole('button', { name: 'Not ekle' }))

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/notes'))
      expect(JSON.parse(String(call?.[1]?.body))).toEqual({ text: 'Dikkat edilmeli' })
    })
  })

  it('sunucu çakışma bildirirse mesajı gösterir', async () => {
    renderDetail(detail(), (url) =>
      url.endsWith('/decision') ? jsonResponse(409, { detail: 'Bu oturum için karar zaten verilmiş.' }) : undefined,
    )
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    await userEvent.click(screen.getByRole('button', { name: 'Temiz' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }))

    expect(await screen.findByText('Bu oturum için karar zaten verilmiş.')).toBeInTheDocument()
  })

  it('saklama süresi dolmuşsa grafik yerine açıklama gösterir', async () => {
    renderDetail(detail({ monitoringDataDeletedAt: '2026-12-25T00:00:00Z', events: [], scoreHistory: [] }))
    await screen.findByRole('heading', { name: 'Ayşe Aday' })

    expect(screen.getByText(/saklama süresi dolduğu için/)).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: /Risk skoru grafiği/ })).not.toBeInTheDocument()
  })
})

describe('sınav listesi', () => {
  it('atanan sınavları bağlantı olarak listeler', async () => {
    mockFetch(() =>
      jsonResponse(200, [{ id: EXAM, name: 'Genel Yetenek', windowStartAt: '2026-11-25T09:00:00Z', windowEndAt: '2026-11-25T18:00:00Z' }]),
    )
    signInAs('Reviewer')
    renderApp('/review')

    expect(await screen.findByRole('link', { name: 'Genel Yetenek' })).toHaveAttribute('href', `/review/exams/${EXAM}`)
  })

  it('atanmış sınav yoksa bunu söyler', async () => {
    mockFetch(() => jsonResponse(200, []))
    signInAs('Reviewer')
    renderApp('/review')

    expect(await screen.findByText('Size atanmış bir sınav yok.')).toBeInTheDocument()
  })

  it('aday inceleme sayfasına giremez', async () => {
    mockFetch(() => jsonResponse(200, []))
    signInAs('Candidate')
    renderApp(`/review/sessions/${SESSION}`)

    expect(await screen.findByRole('heading', { name: 'Sınavlarım' })).toBeInTheDocument()
  })
})
