import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AGENT_PAIR_URL } from '../../api/agentPairing'
import { CONSENT_TEXTS, withCandidateBasics } from '../../test/candidateServer'
import { jsonResponse, mockFetch, type FetchHandler } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'

const SESSION = '11111111-1111-1111-1111-111111111111'
const CODE = 'gizli-kod-1234567890'
const PATH = `/exams/${SESSION}/start`

afterEach(() => {
  vi.unstubAllGlobals()
})

function sessionView(state: string) {
  return {
    id: SESSION,
    examId: 'sinav-1',
    examName: 'Genel Yetenek Deneme',
    state,
    durationMinutes: 45,
    startedAt: null,
    endsAt: null,
    serverTime: '2026-11-25T12:00:00Z',
    questions: [],
    answers: [],
    lastSeq: 0,
  }
}

function agentStatus(overrides: Record<string, unknown> = {}) {
  return {
    state: 'AgentPairing',
    paired: false,
    agentActive: false,
    lastSignalAt: null,
    indicators: [],
    canContinueIndicated: false,
    canContinueWithoutAgent: true,
    ...overrides,
  }
}

const isGet = (init: RequestInit) => (init.method ?? 'GET') === 'GET'

/**
 * Sunucu benzeri: oturum durumu bir değişkende; rıza, eşleştirme ve devam istekleri durumu ilerletir.
 * `extra` testin ilgilendiği uçları değiştirir.
 */
function server(initial: string, extra: FetchHandler = () => undefined as unknown as Response) {
  let state = initial
  const handler: FetchHandler = (url, init) => {
    const custom = extra(url, init)
    if (custom) return custom
    if (url === `/api/sessions/${SESSION}` && isGet(init)) return jsonResponse(200, sessionView(state))
    if (url.endsWith('/consent')) {
      const accepted = JSON.parse(init.body as string).accepted
      state = accepted ? 'AgentPairing' : 'Cancelled'
      return jsonResponse(200, { state })
    }
    if (url.endsWith('/agent') && isGet(init)) return jsonResponse(200, agentStatus({ state }))
    if (url.endsWith('/agent/continue')) {
      state = 'IdentityVerification'
      return jsonResponse(200, { state })
    }
    if (url === '/api/exams/sinav-1') return jsonResponse(200, { id: 'sinav-1', questionCount: 20, latestStartAt: '2026-11-25T17:00:00Z' })
    return jsonResponse(500)
  }
  return handler
}

async function open(handler: FetchHandler) {
  const fetchMock = mockFetch(withCandidateBasics(handler, { enrolled: true }))
  signInAs('Candidate')
  await renderApp(PATH)
  await screen.findByRole('heading', { level: 1, name: 'Genel Yetenek Deneme' })
  return fetchMock
}

const call = (fetchMock: ReturnType<typeof mockFetch>, suffix: string) => fetchMock.mock.calls.find(([url]) => String(url).endsWith(suffix))

async function acceptConsent() {
  await screen.findByText(CONSENT_TEXTS.Session.title)
  await userEvent.click(screen.getByRole('checkbox'))
  await userEvent.click(screen.getByRole('button', { name: 'Kabul ediyorum, devam' }))
  await screen.findByRole('heading', { name: 'Güvenlik ajanını çalıştırın' })
}

describe('sınav öncesi sihirbazı', () => {
  it('rıza onaylanmadan devam edilemez', async () => {
    const fetchMock = await open(server('ConsentPending'))
    await screen.findByText(CONSENT_TEXTS.Session.title)

    expect(screen.getByRole('button', { name: 'Kabul ediyorum, devam' })).toBeDisabled()
    expect(call(fetchMock, '/consent')).toBeUndefined()
    expect(screen.getByRole('list', { name: 'Sınav öncesi adımlar' })).toHaveTextContent('1Aydınlatma ve rıza')
  })

  it('rızayı sunucudaki metnin sürümüyle kaydedip ajan adımına geçer', async () => {
    const fetchMock = await open(server('ConsentPending'))
    await acceptConsent()

    expect(JSON.parse(call(fetchMock, '/consent')![1]!.body as string)).toEqual({ accepted: true, textVersion: CONSENT_TEXTS.Session.version })
  })

  it('rıza reddi onay ister, oturumu iptal eder ve hakkın kullanılmadığını söyler', async () => {
    const fetchMock = await open(server('ConsentPending'))
    await screen.findByText(CONSENT_TEXTS.Session.title)

    await userEvent.click(screen.getByRole('button', { name: 'Rıza vermiyorum' }))
    await userEvent.click(screen.getByRole('button', { name: 'Oturumu iptal et' }))

    expect(await screen.findByRole('heading', { name: 'Oturum iptal edildi' })).toBeInTheDocument()
    expect(screen.getByText('Sınav hakkınız kullanılmadı.')).toBeInTheDocument()
    expect(JSON.parse(call(fetchMock, '/consent')![1]!.body as string)).toMatchObject({ accepted: false })
  })

  it('rıza 409 dönerse sunucunun nedenini gösterir', async () => {
    await open(server('ConsentPending', (url) => (url.endsWith('/consent') ? jsonResponse(409, { title: 'Kayıt fotoğrafı yüklenmeden sınava başlanamaz.' }) : undefined!)))
    await screen.findByText(CONSENT_TEXTS.Session.title)

    await userEvent.click(screen.getByRole('checkbox'))
    await userEvent.click(screen.getByRole('button', { name: 'Kabul ediyorum, devam' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Kayıt fotoğrafı yüklenmeden sınava başlanamaz.')
  })

  it('sayfa yeniden açılınca oturumun bulunduğu adımdan devam eder', async () => {
    await open(server('AgentPairing'))

    expect(await screen.findByRole('heading', { name: 'Güvenlik ajanını çalıştırın' })).toBeInTheDocument()
  })

  it('kodu ekrana yazmadan ajana gönderir; sunucu ilerleyince hazırlık adımına geçer', async () => {
    let paired = false
    const fetchMock = await open(
      server('AgentPairing', (url, init) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-25T12:02:00Z' })
        if (url === AGENT_PAIR_URL) {
          paired = true
          return jsonResponse(200, { status: 'paired' })
        }
        if (url.endsWith('/agent') && isGet(init) && paired) {
          return jsonResponse(200, agentStatus({ state: 'IdentityVerification', paired: true, agentActive: true, canContinueWithoutAgent: false }))
        }
        return undefined!
      }),
    )
    await screen.findByRole('heading', { name: 'Güvenlik ajanını çalıştırın' })
    await userEvent.click(screen.getByRole('button', { name: 'Ajanla eşleştir' }))

    expect(await screen.findByRole('heading', { name: 'Sınava başlamadan önce' })).toBeInTheDocument()
    expect(screen.queryByText(CODE)).not.toBeInTheDocument()
    expect(JSON.parse(fetchMock.mock.calls.find(([url]) => url === AGENT_PAIR_URL)![1]!.body as string)).toEqual({ code: CODE })
    expect(screen.getByText('Güvenlik ajanı eşleşti')).toBeInTheDocument()
  })

  it('ajan bulunamazsa nedenini söyler ve gerekçeyle noAgent devam eder', async () => {
    const fetchMock = await open(
      server('AgentPairing', (url) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-25T12:02:00Z' })
        if (url === AGENT_PAIR_URL) throw new TypeError('Failed to fetch')
        return undefined!
      }),
    )
    await userEvent.click(await screen.findByRole('button', { name: 'Ajanla eşleştir' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Güvenlik ajanı bulunamadı')

    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))
    expect(screen.getByText('Devam etmek için gerekçe yazın.')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Gerekçe'), 'Ajan bu bilgisayarda açılmıyor.')
    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))

    expect(await screen.findByRole('heading', { name: 'Sınava başlamadan önce' })).toBeInTheDocument()
    expect(JSON.parse(call(fetchMock, '/agent/continue')![1]!.body as string)).toEqual({ mode: 'noAgent', reason: 'Ajan bu bilgisayarda açılmıyor.' })
  })

  it('göstergelerin nedenini ve çözümünü gösterir; ajansız devamı gizler, gerekçeyi indicated ile gönderir', async () => {
    const fetchMock = await open(
      server('AgentPairing', (url, init) =>
        url.endsWith('/agent') && isGet(init)
          ? jsonResponse(
              200,
              agentStatus({
                paired: true,
                agentActive: true,
                canContinueWithoutAgent: false,
                canContinueIndicated: true,
                indicators: [{ type: 'FORBIDDEN_APP', name: 'AnyDesk', message: 'Yasaklı uygulama çalışıyor: AnyDesk.', fix: 'AnyDesk uygulamasını tamamen kapatın.' }],
              }),
            )
          : undefined!,
      ),
    )

    expect(await screen.findByText('Yasaklı uygulama çalışıyor: AnyDesk.')).toBeInTheDocument()
    expect(screen.getByText('AnyDesk uygulamasını tamamen kapatın.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Düzelttim, tekrar kontrol et' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ajansız devam et' })).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Gerekçe'), 'Kapatamıyorum.')
    await userEvent.click(screen.getByRole('button', { name: 'Gerekçeyle devam et' }))

    await screen.findByRole('heading', { name: 'Sınava başlamadan önce' })
    expect(JSON.parse(call(fetchMock, '/agent/continue')![1]!.body as string)).toEqual({ mode: 'indicated', reason: 'Kapatamıyorum.' })
  })

  it('ajansız devam 409 alınca adımda kalır ve ajanın çalıştığını söylemez', async () => {
    await open(server('AgentPairing', (url) => (url.endsWith('/agent/continue') ? jsonResponse(409, { detail: 'Güvenlik ajanı çalışıyor; ajansız devam edilemez.' }) : undefined!)))
    await userEvent.type(await screen.findByLabelText('Gerekçe'), 'Ajan açılmıyor.')
    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))

    expect(await screen.findByText('Ajansız devam edilemiyor.')).toBeInTheDocument()
    expect(screen.queryByText(/çalışıyor;/)).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Güvenlik ajanını çalıştırın' })).toBeInTheDocument()
  })

  it('hazırlık adımında süre işlemez; kurallar ve sınav bilgisi sunucudan gelir', async () => {
    await open(server('IdentityVerification'))

    expect(await screen.findByRole('heading', { name: 'Sınava başlamadan önce' })).toBeInTheDocument()
    expect(await screen.findByText('20')).toBeInTheDocument()
    expect(screen.getByText('Süre kimliğiniz doğrulanınca başlar')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Kimlik doğrulamaya geç' }))
    expect(screen.getByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
  })

  it('doğrulama başarısızsa sunucunun nedenini ve destek talebini gösterir', async () => {
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn(async () => ({ getTracks: () => [{ stop: vi.fn() }] })) },
    })
    HTMLMediaElement.prototype.play = () => Promise.resolve()
    HTMLCanvasElement.prototype.getContext = (() => ({ drawImage() {} })) as unknown as typeof HTMLCanvasElement.prototype.getContext
    HTMLCanvasElement.prototype.toBlob = (callback) => callback(new Blob(['kare'], { type: 'image/jpeg' }))

    await open(
      server('IdentityVerification', (url) =>
        url.endsWith('/verify') ? jsonResponse(422, { title: 'Kimlik doğrulanamadı.', detail: 'Yüzünüzün net göründüğünden emin olup tekrar deneyin.' }) : undefined!,
      ),
    )
    await userEvent.click(await screen.findByRole('button', { name: 'Kimlik doğrulamaya geç' }))
    await userEvent.click(screen.getByRole('button', { name: 'Kamerayı aç' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Yüzümü doğrula ve sınavı başlat' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Kimlik doğrulanamadı.')
    expect(alert).toHaveTextContent('net göründüğünden')
    await userEvent.click(screen.getByRole('button', { name: 'Destek talep et' }))
    expect(screen.getByRole('dialog')).toHaveTextContent(SESSION)
  })

  it('sınav sürüyorsa doğrudan sınav ekranına gider', async () => {
    mockFetch(withCandidateBasics(server('InProgress'), { enrolled: true }))
    signInAs('Candidate')
    await renderApp(PATH)

    // Sınav ekranı açılır: sihirbazın adım listesi hiç çizilmez.
    await screen.findByRole('timer', { name: 'Kalan süre' })
    expect(screen.queryByRole('list', { name: 'Sınav öncesi adımlar' })).not.toBeInTheDocument()
  })
})
