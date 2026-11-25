import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AGENT_PAIR_URL } from '../api/agentPairing'
import { jsonResponse, mockFetch, type FetchHandler } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'

const SESSION = '11111111-1111-1111-1111-111111111111'
const CODE = 'gizli-kod-1234567890'
const PATH = `/exams/${SESSION}/start`

afterEach(() => {
  vi.unstubAllGlobals()
})

async function renderWizard(handler: FetchHandler) {
  const fetchMock = mockFetch(handler)
  signInAs('Candidate')
  await renderApp(PATH)
  return fetchMock
}

async function acceptConsent() {
  await userEvent.click(await screen.findByRole('checkbox'))
  await userEvent.click(screen.getByRole('button', { name: 'Devam' }))
  expect(await screen.findByRole('button', { name: 'Ajanla eşleştir' })).toBeInTheDocument()
}

/** Sunucudaki gibi: rıza kaydedilene kadar oturum ConsentPending görünür, sonrası `handler`a kalır. */
function consentOr(handler: FetchHandler): FetchHandler {
  let consented = false
  return (url, init) => {
    if (url.includes('/consent')) {
      consented = true
      return jsonResponse(204)
    }
    if (!consented && isAgentStatusGet(url, init)) {
      return jsonResponse(200, agentStatus({ state: 'ConsentPending', canContinueWithoutAgent: false }))
    }
    return handler(url, init)
  }
}

function consentCall(fetchMock: ReturnType<typeof mockFetch>) {
  return fetchMock.mock.calls.find(([url]) => String(url).includes('/consent'))
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

function isAgentStatusGet(url: string, init?: RequestInit): boolean {
  return url.endsWith('/agent') && (init?.method ?? 'GET') === 'GET'
}

describe('sınav öncesi sihirbazı', () => {
  it('rıza onaylanmadan devam etmez ve kamerayı göstermez', async () => {
    const fetchMock = await renderWizard(() => jsonResponse(204))

    await userEvent.click(await screen.findByRole('button', { name: 'Devam' }))
    expect(screen.queryByRole('button', { name: 'Kamerayı aç' })).not.toBeInTheDocument()

    expect(screen.getByRole('alert')).toHaveTextContent('aydınlatma metnini onaylayın')
    expect(consentCall(fetchMock)).toBeUndefined()
  })

  it('açık rızayı oturuma kaydedip ajan adımına geçer', async () => {
    const fetchMock = await renderWizard(() => jsonResponse(204))

    await acceptConsent()

    const call = consentCall(fetchMock)
    expect(call?.[0]).toBe(`/api/sessions/${SESSION}/consent`)
    expect(JSON.parse(call?.[1]?.body as string)).toEqual({
      accepted: true,
      textVersion: 'kvkk-session-v1',
    })
  })

  it('rıza reddedilince oturumun iptal edildiğini söyler', async () => {
    const fetchMock = await renderWizard(() => jsonResponse(204))

    await userEvent.click(await screen.findByRole('button', { name: 'Rızayı reddet' }))

    expect(await screen.findByText(/oturum iptal edildi/)).toBeInTheDocument()
    expect(JSON.parse(consentCall(fetchMock)?.[1]?.body as string)).toEqual({
      accepted: false,
      textVersion: 'kvkk-session-v1',
    })
  })

  it('rıza 409 dönerse sunucunun bildirdiği nedeni gösterir', async () => {
    await renderWizard((url) =>
      url.includes('/consent')
        ? jsonResponse(409, { title: 'Kayıt fotoğrafı yüklenmeden sınava başlanamaz.', status: 409 })
        : jsonResponse(204),
    )

    await userEvent.click(await screen.findByRole('checkbox'))
    await userEvent.click(screen.getByRole('button', { name: 'Devam' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Kayıt fotoğrafı yüklenmeden sınava başlanamaz.')
    expect(screen.queryByRole('button', { name: 'Ajanla eşleştir' })).not.toBeInTheDocument()
  })

  it('rızası alınmış oturumda sayfa yeniden açılınca ajan adımından devam eder', async () => {
    const fetchMock = await renderWizard((url, init) =>
      isAgentStatusGet(url, init) ? jsonResponse(200, agentStatus()) : jsonResponse(500),
    )

    expect(await screen.findByRole('button', { name: 'Ajanla eşleştir' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(consentCall(fetchMock)).toBeUndefined()
  })

  it('kimlik adımındaki oturumda sayfa yeniden açılınca kamera adımından devam eder', async () => {
    await renderWizard((url, init) =>
      isAgentStatusGet(url, init)
        ? jsonResponse(200, agentStatus({ state: 'IdentityVerification', canContinueWithoutAgent: false }))
        : jsonResponse(500),
    )

    expect(await screen.findByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  it('eşleşmeden sonra sunucu kimlik adımına geçince kameraya gider', async () => {
    let paired = false
    const fetchMock = await renderWizard(
      consentOr((url, init) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) {
          paired = true
          return jsonResponse(200, { status: 'paired' })
        }
        if (isAgentStatusGet(url, init)) {
          return jsonResponse(
            200,
            paired
              ? agentStatus({ state: 'IdentityVerification', paired: true, agentActive: true, canContinueWithoutAgent: false })
              : agentStatus(),
          )
        }
        return jsonResponse(500)
      }),
    )
    await acceptConsent()
    expect(screen.getByText(/aiguard-agent.exe/)).toBeInTheDocument()
    expect(screen.getByText(/Yine de çalıştır/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ajanla eşleştir' }))

    expect(await screen.findByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
    expect(screen.queryByText(CODE)).not.toBeInTheDocument()
    const pairCall = fetchMock.mock.calls.find(([url]) => url === AGENT_PAIR_URL)
    expect(JSON.parse(pairCall?.[1]?.body as string)).toEqual({ code: CODE })
    expect(fetchMock.mock.calls.some(([url, init]) => isAgentStatusGet(String(url), init))).toBe(true)
  })

  it('ajan bulunamazsa gerekçeyi noAgent ile gönderir', async () => {
    const fetchMock = await renderWizard(
      consentOr((url, init) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) throw new TypeError('Failed to fetch')
        if (url.endsWith('/agent/continue')) return jsonResponse(200, { state: 'IdentityVerification' })
        if (isAgentStatusGet(url, init)) return jsonResponse(200, agentStatus())
        return jsonResponse(500)
      }),
    )
    await acceptConsent()
    await userEvent.click(screen.getByRole('button', { name: 'Ajanla eşleştir' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Güvenlik ajanı bulunamadı')
    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))
    expect(screen.getByText('Devam etmek için gerekçe yazın.')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Gerekçe'), 'Ajan bu bilgisayarda kurulu değil.')
    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))

    expect(await screen.findByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
    const skip = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/agent/continue'))
    expect(JSON.parse(skip?.[1]?.body as string)).toEqual({ mode: 'noAgent', reason: 'Ajan bu bilgisayarda kurulu değil.' })
  })

  it('göstergelerin nedenini ve çözümünü gösterir, gerekçeyi indicated ile gönderir', async () => {
    const fetchMock = await renderWizard(
      consentOr((url, init) => {
        if (url.endsWith('/agent/continue')) return jsonResponse(200, { state: 'IdentityVerification' })
        if (isAgentStatusGet(url, init)) {
          return jsonResponse(
            200,
            agentStatus({
              paired: true,
              agentActive: true,
              canContinueWithoutAgent: false,
              canContinueIndicated: true,
              indicators: [
                {
                  type: 'FORBIDDEN_APP',
                  name: 'AnyDesk',
                  message: 'Sınav sırasında açık olmaması gereken bir uygulama çalışıyor: AnyDesk.',
                  fix: 'AnyDesk uygulamasını tamamen kapatın.',
                },
              ],
            }),
          )
        }
        return jsonResponse(500)
      }),
    )
    await acceptConsent()

    expect(await screen.findByText(/uygulama çalışıyor: AnyDesk/)).toBeInTheDocument()
    expect(screen.getByText('AnyDesk uygulamasını tamamen kapatın.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ajansız devam et' })).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Gerekçe'), 'Uygulamayı kapatamıyorum.')
    await userEvent.click(screen.getByRole('button', { name: 'Gerekçeyle devam et' }))

    expect(await screen.findByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
    const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/agent/continue'))
    expect(JSON.parse(call?.[1]?.body as string)).toEqual({ mode: 'indicated', reason: 'Uygulamayı kapatamıyorum.' })
  })

  it('ajan varken ajansız devamı gizler', async () => {
    await renderWizard(
      consentOr((url, init) => {
        if (isAgentStatusGet(url, init)) {
          return jsonResponse(200, agentStatus({ paired: true, agentActive: true, canContinueWithoutAgent: false }))
        }
        return jsonResponse(500)
      }),
    )
    await acceptConsent()

    expect(await screen.findByText('Eşleşme alındı. Denetim birkaç saniye içinde yenilenir.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ajansız devam et' })).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('ajansız devam 409 alınca adımda kalır ve ajanın çalıştığını söylemez', async () => {
    await renderWizard(
      consentOr((url, init) => {
        if (url.endsWith('/agent/continue')) {
          return jsonResponse(409, { detail: 'Güvenlik ajanı çalışıyor; ajansız devam edilemez.' })
        }
        if (isAgentStatusGet(url, init)) return jsonResponse(200, agentStatus())
        return jsonResponse(500)
      }),
    )
    await acceptConsent()
    await userEvent.type(await screen.findByLabelText('Gerekçe'), 'Ajan açılmıyor.')
    await userEvent.click(screen.getByRole('button', { name: 'Ajansız devam et' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Ajansız devam edilemiyor.')
    expect(screen.getByRole('alert')).not.toHaveTextContent('çalışıyor')
    expect(screen.queryByRole('button', { name: 'Kamerayı aç' })).not.toBeInTheDocument()
  })

  it('doğrulama başarısızsa yeniden deneme ve destek talebini gösterir', async () => {
    const stop = vi.fn()
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn(async () => ({ getTracks: () => [{ stop }] })) },
    })
    HTMLMediaElement.prototype.play = () => Promise.resolve()
    HTMLCanvasElement.prototype.getContext = (() => ({ drawImage() {} })) as unknown as typeof HTMLCanvasElement.prototype.getContext
    HTMLCanvasElement.prototype.toBlob = (callback) => {
      callback(new Blob(['kare'], { type: 'image/jpeg' }))
    }

    let paired = false
    await renderWizard(
      consentOr((url, init) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) {
          paired = true
          return jsonResponse(200, { status: 'paired' })
        }
        if (isAgentStatusGet(url, init)) {
          return jsonResponse(
            200,
            paired
              ? agentStatus({ state: 'IdentityVerification', paired: true, agentActive: true, canContinueWithoutAgent: false })
              : agentStatus(),
          )
        }
        if (url.includes('/verify')) return jsonResponse(200, { passed: false })
        return jsonResponse(500)
      }),
    )
    await acceptConsent()
    await userEvent.click(screen.getByRole('button', { name: 'Ajanla eşleştir' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Kamerayı aç' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Doğrulamayı başlat' })).toBeEnabled())
    await userEvent.click(screen.getByRole('button', { name: 'Doğrulamayı başlat' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Kimlik doğrulanamadı')
    await userEvent.click(screen.getByRole('button', { name: 'Destek talep et' }))
    expect(await screen.findByRole('dialog')).toHaveTextContent(SESSION)
    expect(screen.getByRole('button', { name: 'Doğrulamayı başlat' })).toBeInTheDocument()
  })
})
