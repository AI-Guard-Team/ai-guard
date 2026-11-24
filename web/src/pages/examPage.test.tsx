import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SESSION_POLL_MS } from '../api/sessions'
import type { PendingAnswer } from '../exam/answerQueue'
import { jsonResponse, mockFetch, type FetchHandler } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'

const SESSION = '22222222-2222-2222-2222-222222222222'
const QUESTION = '33333333-3333-3333-3333-333333333333'
const PATH = `/exams/${SESSION}`

const queue = vi.hoisted(() => ({ items: [] as PendingAnswer[] }))

vi.mock('../exam/answerQueue', async () => {
  const actual = await vi.importActual<typeof import('../exam/answerQueue')>('../exam/answerQueue')
  return {
    ...actual,
    listPending: vi.fn(async (sessionId: string) => queue.items.filter((item) => item.sessionId === sessionId)),
    enqueue: vi.fn(async (answer: PendingAnswer) => {
      queue.items.push(answer)
    }),
    removePending: vi.fn(async (answer: Pick<PendingAnswer, 'sessionId' | 'seq'>) => {
      queue.items = queue.items.filter((item) => item.sessionId !== answer.sessionId || item.seq !== answer.seq)
    }),
  }
})

// Kare jsdom'da canvas olmadığı için sahte; ses kaydı (MediaRecorder) jsdom'da yok, ayrı birim testinde.
vi.mock('../exam/mediaCapture', async () => {
  const actual = await vi.importActual<typeof import('../exam/mediaCapture')>('../exam/mediaCapture')
  return {
    ...actual,
    createBrowserCapture: () => ({
      captureFrame: async () => new Blob(['jpeg'], { type: 'image/jpeg' }),
      createRecorder: () => null,
      dispose: () => undefined,
    }),
  }
})

class FakeTrack extends EventTarget {
  readyState: 'live' | 'ended' = 'live'
  readonly kind: string
  constructor(kind: string) {
    super()
    this.kind = kind
  }
  stop() {
    this.readyState = 'ended'
  }
  /** Tarayıcıda izin kaldırılınca olduğu gibi. */
  revoke() {
    this.readyState = 'ended'
    this.dispatchEvent(new Event('ended'))
  }
}

const media = { tracks: [] as FakeTrack[], getUserMedia: vi.fn() }

function fakeStream() {
  const tracks = [new FakeTrack('video'), new FakeTrack('audio')]
  media.tracks.push(...tracks)
  return {
    getTracks: () => tracks,
    getAudioTracks: () => tracks.filter((track) => track.kind === 'audio'),
  } as unknown as MediaStream
}

function setGetUserMedia(impl: () => Promise<MediaStream>) {
  media.getUserMedia = vi.fn(impl)
  Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: { getUserMedia: media.getUserMedia } })
}

function session(overrides: Record<string, unknown> = {}) {
  return {
    id: SESSION,
    examId: '44444444-4444-4444-4444-444444444444',
    examName: 'Genel Yetenek',
    state: 'InProgress',
    durationMinutes: 20,
    startedAt: '2026-11-21T10:00:00.000Z',
    endsAt: '2026-11-21T10:12:30.000Z',
    serverTime: '2026-11-21T10:00:00.000Z',
    questions: [{ id: QUESTION, text: '2 + 2 kaçtır?', options: ['3', '4'], order: 1 }],
    answers: [],
    lastSeq: 4,
    ...overrides,
  }
}

function renderExam(handler: FetchHandler) {
  const fetchMock = mockFetch(handler)
  signInAs('Candidate')
  renderApp(PATH)
  return fetchMock
}

describe('sınav ekranı', () => {
  beforeEach(() => {
    queue.items = []
    media.tracks = []
    setGetUserMedia(async () => fakeStream())
  })

  it('kalan süreyi sunucunun bitiş ve saatinden gösterir', async () => {
    renderExam(() => jsonResponse(200, session()))

    expect(await screen.findByRole('heading', { name: 'Genel Yetenek' })).toBeInTheDocument()
    expect(screen.getByText('Kalan süre 12:30')).toBeInTheDocument()
    expect(await screen.findByText('Kamera Açık')).toBeInTheDocument()
    expect(screen.getByText('Bağlantı Bağlı')).toBeInTheDocument()
    expect(screen.getByText(/2 \+ 2 kaçtır/)).toBeInTheDocument()
  })

  it('ajan çalışıyorsa üst çubukta gösterir', async () => {
    renderExam((url) =>
      String(url).endsWith('/agent')
        ? jsonResponse(200, {
            state: 'InProgress',
            paired: true,
            agentActive: true,
            indicators: [],
            canContinueIndicated: false,
            canContinueWithoutAgent: false,
          })
        : jsonResponse(200, session()),
    )

    expect(await screen.findByText('Ajan Çalışıyor')).toBeInTheDocument()
  })

  it('tek soru gösterir ve alttan diğer soruya geçer', async () => {
    const second = '55555555-5555-5555-5555-555555555555'
    renderExam(() =>
      jsonResponse(
        200,
        session({
          questions: [
            { id: QUESTION, text: '2 + 2 kaçtır?', options: ['3', '4'], order: 1 },
            { id: second, text: 'Başkent neresidir?', options: ['Ankara', 'İzmir'], order: 2 },
          ],
        }),
      ),
    )

    expect(await screen.findByText(/2 \+ 2 kaçtır/)).toBeInTheDocument()
    expect(screen.queryByText(/Başkent neresidir/)).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Sonraki' }))
    expect(screen.getByText(/Başkent neresidir/)).toBeInTheDocument()
    expect(screen.queryByText(/2 \+ 2 kaçtır/)).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: '1' }))
    expect(screen.getByText(/2 \+ 2 kaçtır/)).toBeInTheDocument()
  })

  it('kayıtlı cevabı seçili getirir ve yeni seçimi sıradaki seq ile gönderir', async () => {
    const fetchMock = renderExam((url) => {
      if (url.endsWith('/answers')) return jsonResponse(201)
      return jsonResponse(200, session({ answers: [{ questionId: QUESTION, selectedOption: 0, seq: 4 }] }))
    })

    expect(await screen.findByRole('radio', { name: '3' })).toBeChecked()
    await userEvent.click(screen.getByRole('radio', { name: '4' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/answers'))).toBe(true))
    const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/answers'))
    const body = JSON.parse(call?.[1]?.body as string)
    expect(body).toMatchObject({ questionId: QUESTION, selectedOption: 1, seq: 5 })
    expect(body.clientTime).toEqual(expect.any(String))
    expect(screen.getByRole('radio', { name: '4' })).toBeChecked()
    expect(screen.queryByText(/sıra/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Gönderilmeyi bekleyen/)).not.toBeInTheDocument()
  })

  it('bağlantı varken bekleyen cevap yazısını göstermez', async () => {
    let release: (value?: void | PromiseLike<void>) => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    renderExam(async (url) => {
      if (String(url).endsWith('/answers')) {
        await gate
        return jsonResponse(201)
      }
      return jsonResponse(200, session())
    })

    await userEvent.click(await screen.findByRole('radio', { name: '4' }))
    await waitFor(() => expect(queue.items).toHaveLength(1))
    expect(screen.getByRole('radio', { name: '4' })).toBeChecked()
    expect(screen.queryByText(/Gönderilmeyi bekleyen/)).not.toBeInTheDocument()
    release()
    await waitFor(() => expect(queue.items).toHaveLength(0))
    expect(screen.queryByText(/Gönderilmeyi bekleyen/)).not.toBeInTheDocument()
  })

  it('gönderilemeyen cevabı bekliyor diye gösterir', async () => {
    renderExam((url) => {
      if (String(url).endsWith('/answers')) throw new TypeError('offline')
      return jsonResponse(200, session())
    })

    await userEvent.click(await screen.findByRole('radio', { name: '4' }))

    expect(await screen.findByText('Cevap bu cihaza kaydedildi. Bağlantı gelince gönderilecek.')).toBeInTheDocument()
    expect(screen.queryByText(/Gönderilmeyi bekleyen/)).not.toBeInTheDocument()
  })

  it('sunucu kapalıysa cevabı kuyrukta tutar', async () => {
    renderExam((url) => (String(url).endsWith('/answers') ? jsonResponse(502) : jsonResponse(200, session())))

    await userEvent.click(await screen.findByRole('radio', { name: '4' }))

    expect(await screen.findByText('Cevap bu cihaza kaydedildi. Bağlantı gelince gönderilecek.')).toBeInTheDocument()
    expect(screen.queryByText(/Gönderilmeyi bekleyen/)).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('her yeni seçimde bir sonraki sıra numarasını gönderir', async () => {
    const fetchMock = renderExam((url) =>
      String(url).endsWith('/answers')
        ? jsonResponse(201)
        : jsonResponse(
            200,
            session({
              questions: [{ id: QUESTION, text: '2 + 2 kaçtır?', options: ['3', '4', '5'], order: 1 }],
            }),
          ),
    )

    await userEvent.click(await screen.findByRole('radio', { name: '4' }))
    await userEvent.click(await screen.findByRole('radio', { name: '5' }))

    await waitFor(() => {
      const sequences = fetchMock.mock.calls
        .filter(([url]) => String(url).endsWith('/answers'))
        .map(([, init]) => JSON.parse(init?.body as string).seq as number)
      expect(sequences).toEqual([5, 6])
    })
  })

  it('oturumu 10 saniyede bir yeniler', async () => {
    const intervals = vi.spyOn(window, 'setInterval')
    const fetchMock = renderExam(() => jsonResponse(200, session()))

    await screen.findByText(/2 \+ 2 kaçtır/)
    const poll = intervals.mock.calls.find(([, delay]) => delay === SESSION_POLL_MS)
    expect(poll?.[1]).toBe(10_000)
    const gets = () =>
      fetchMock.mock.calls.filter(([url, init]) => {
        const method = init?.method ?? 'GET'
        return method === 'GET' && String(url) === `/api/sessions/${SESSION}`
      }).length
    const before = gets()
    ;(poll?.[0] as () => void)()
    await waitFor(() => expect(gets()).toBe(before + 1))
    intervals.mockRestore()
  })

  it('bağlantı kesilince kuyruğu yeniden doğrulamadan önce gönderir', async () => {
    queue.items = [
      {
        sessionId: SESSION,
        questionId: QUESTION,
        selectedOption: 1,
        seq: 5,
        clientTime: '2026-11-21T10:01:00.000Z',
      },
    ]
    const order: string[] = []
    renderExam((url, init) => {
      const path = String(url)
      order.push(`${init?.method ?? 'GET'} ${path}`)
      if (path.endsWith('/answers')) return jsonResponse(201)
      return jsonResponse(
        200,
        session({
          state: 'Disconnected',
          questions: [],
          answers: [{ questionId: QUESTION, selectedOption: 1, seq: 5 }],
          lastSeq: 5,
        }),
      )
    })

    expect(await screen.findByRole('button', { name: 'Kimliği yeniden doğrula' })).toBeInTheDocument()
    const answerAt = order.findIndex((entry) => entry.startsWith('POST') && entry.endsWith('/answers'))
    const verifyAt = order.findIndex((entry) => entry.includes('/verify'))
    expect(answerAt).toBeGreaterThanOrEqual(0)
    expect(verifyAt).toBe(-1)
    expect(order.findIndex((entry) => entry.startsWith('GET'))).toBeLessThan(answerAt)
  })

  it('cevaptaki 409 ayrıntısını gösterir', async () => {
    renderExam((url) =>
      String(url).endsWith('/answers')
        ? jsonResponse(409, { detail: 'Sınav süresi doldu; cevap kabul edilmedi.' })
        : jsonResponse(200, session()),
    )

    await userEvent.click(await screen.findByRole('radio', { name: '4' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Sınav süresi doldu')
  })

  it('teslimden önce kuyruğu gönderir', async () => {
    queue.items = [
      {
        sessionId: SESSION,
        questionId: QUESTION,
        selectedOption: 0,
        seq: 5,
        clientTime: '2026-11-21T10:01:00.000Z',
      },
    ]
    let submitted = false
    const order: string[] = []
    renderExam((url, init) => {
      const path = String(url)
      const method = init?.method ?? 'GET'
      order.push(`${method} ${path}`)
      if (path.endsWith('/answers')) return jsonResponse(201)
      if (path.endsWith('/submit')) {
        submitted = true
        return jsonResponse(204)
      }
      return jsonResponse(200, submitted ? session({ state: 'Completed', questions: [] }) : session())
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Sınavı teslim et' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Teslim et' }))

    expect(await screen.findByText('Sınav teslim edildi.')).toBeInTheDocument()
    const answerAt = order.findIndex((entry) => entry.endsWith('/answers'))
    const submitAt = order.findIndex((entry) => entry.endsWith('/submit'))
    expect(answerAt).toBeGreaterThanOrEqual(0)
    expect(answerAt).toBeLessThan(submitAt)
  })

  describe('sınav sırası izleme (S4)', () => {
    const calls = (fetchMock: ReturnType<typeof mockFetch>, suffix: string) =>
      fetchMock.mock.calls.filter(([url, init]) => String(url).endsWith(suffix) && init?.method === 'POST')

    const bodyOf = (call: unknown[] | undefined) => JSON.parse((call?.[1] as RequestInit).body as string)

    const allStopped = () => media.tracks.every((track) => track.readyState === 'ended')

    it('sınav sürerken kamera ve mikrofonu açar ve saniyede bir kare gönderir', async () => {
      const fetchMock = renderExam((url) => (url.endsWith('/frames') ? jsonResponse(202) : jsonResponse(200, session())))

      await waitFor(() => expect(calls(fetchMock, '/frames').length).toBeGreaterThanOrEqual(1), { timeout: 2_500 })
      expect(media.getUserMedia).toHaveBeenCalledWith({ video: { facingMode: 'user' }, audio: true })
      const body = calls(fetchMock, '/frames')[0][1]?.body
      expect(body).toBeInstanceOf(FormData)
      expect((body as FormData).get('frame')).toBeInstanceOf(Blob)
      // Kare ve ses tarayıcıda saklanmaz.
      expect(localStorage.length).toBe(0)
      expect(sessionStorage.length).toBe(0)
      expect(queue.items).toHaveLength(0)
    })

    it('izin kalkınca sunucuya bildirir, soruları gizler, süreyi gösterir ve izin verilince döner', async () => {
      let state = 'InProgress'
      const fetchMock = renderExam((url, init) => {
        if (url.endsWith('/permission')) {
          state = JSON.parse(init.body as string).granted ? 'InProgress' : 'PermissionPending'
          return jsonResponse(200, { state })
        }
        if (url.endsWith('/frames')) return jsonResponse(202)
        return jsonResponse(200, session(state === 'InProgress' ? {} : { state, questions: [] }))
      })
      await screen.findByText('Kamera Açık')

      media.tracks[0].revoke()

      expect(await screen.findByRole('button', { name: 'Kamera ve mikrofona izin ver' })).toBeInTheDocument()
      expect(bodyOf(calls(fetchMock, '/permission')[0])).toEqual({ granted: false })
      expect(screen.queryByText(/2 \+ 2 kaçtır/)).not.toBeInTheDocument()
      expect(screen.getByText(/Süre işlemeye devam ediyor/)).toBeInTheDocument()
      expect(screen.getByText(/Kalan süre 12:/)).toBeInTheDocument()
      // İzin kalkınca kamera ışığı söner.
      expect(allStopped()).toBe(true)

      await userEvent.click(screen.getByRole('button', { name: 'Kamera ve mikrofona izin ver' }))

      expect(await screen.findByText(/2 \+ 2 kaçtır/)).toBeInTheDocument()
      // İzinsiz süreyi sunucu ölçer; istemci süre göndermez (K-30).
      expect(bodyOf(calls(fetchMock, '/permission')[1])).toEqual({ granted: true })
    })

    it('sınav başında izin verilmezse sınavı izin beklemeye alır', async () => {
      setGetUserMedia(() => Promise.reject(new DOMException('izin yok', 'NotAllowedError')))
      const fetchMock = renderExam((url) =>
        url.endsWith('/permission') ? jsonResponse(200, { state: 'PermissionPending' }) : jsonResponse(200, session()),
      )

      await waitFor(() => expect(calls(fetchMock, '/permission').length).toBeGreaterThanOrEqual(1))
      expect(bodyOf(calls(fetchMock, '/permission')[0])).toEqual({ granted: false })
    })

    it('tarayıcı olaylarını türü ve süresiyle gönderir', async () => {
      const fetchMock = renderExam((url) => (url.endsWith('/events') ? jsonResponse(201) : jsonResponse(200, session())))
      await screen.findByText(/2 \+ 2 kaçtır/)

      fireEvent.contextMenu(document.body)
      fireEvent.copy(document.body)

      await waitFor(() => expect(calls(fetchMock, '/events')).toHaveLength(2))
      expect(calls(fetchMock, '/events').map(bodyOf)).toEqual([
        { type: 'RIGHT_CLICK', durationSec: 0 },
        { type: 'COPY_PASTE', durationSec: 0 },
      ])
    })

    it('bağlantı kesikken izleme ve olay gönderimi yapmaz', async () => {
      const fetchMock = renderExam(() => jsonResponse(200, session({ state: 'Disconnected', questions: [] })))
      await screen.findByRole('button', { name: 'Kimliği yeniden doğrula' })

      fireEvent.contextMenu(document.body)
      await new Promise((resolve) => setTimeout(resolve, 1_200))

      expect(media.getUserMedia).not.toHaveBeenCalled()
      expect(calls(fetchMock, '/frames')).toHaveLength(0)
      expect(calls(fetchMock, '/events')).toHaveLength(0)
    })

    it('teslimden sonra kamerayı kapatır', async () => {
      let submitted = false
      renderExam((url) => {
        if (url.endsWith('/submit')) {
          submitted = true
          return jsonResponse(204)
        }
        if (url.endsWith('/frames')) return jsonResponse(202)
        return jsonResponse(200, submitted ? session({ state: 'Completed', questions: [] }) : session())
      })
      await screen.findByText('Kamera Açık')

      await userEvent.click(screen.getByRole('button', { name: 'Sınavı teslim et' }))
      await userEvent.click(await screen.findByRole('button', { name: 'Teslim et' }))

      await screen.findByText('Sınav teslim edildi.')
      await waitFor(() => expect(allStopped()).toBe(true))
    })
  })
})
