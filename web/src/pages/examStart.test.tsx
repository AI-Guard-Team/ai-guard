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

function renderWizard(handler: FetchHandler) {
  const fetchMock = mockFetch(handler)
  signInAs('Candidate')
  renderApp(PATH)
  return fetchMock
}

async function acceptConsent() {
  await userEvent.click(screen.getByRole('checkbox'))
  await userEvent.click(screen.getByRole('button', { name: 'Devam' }))
  expect(await screen.findByRole('button', { name: 'Ajanla eşleştir' })).toBeInTheDocument()
}

function consentOr(handler: FetchHandler): FetchHandler {
  return (url, init) => {
    if (url.includes('/consent')) return jsonResponse(204)
    return handler(url, init)
  }
}

describe('sınav öncesi sihirbazı', () => {
  it('rıza onaylanmadan devam etmez ve kamerayı göstermez', async () => {
    const fetchMock = renderWizard(() => jsonResponse(204))

    expect(screen.queryByRole('button', { name: 'Kamerayı aç' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Devam' }))

    expect(screen.getByRole('alert')).toHaveTextContent('aydınlatma metnini onaylayın')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('açık rızayı oturuma kaydedip ajan adımına geçer', async () => {
    const fetchMock = renderWizard(() => jsonResponse(204))

    await acceptConsent()

    expect(fetchMock.mock.calls[0][0]).toBe(`/api/sessions/${SESSION}/consent`)
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({
      granted: true,
      textVersion: 'kvkk-session-v1',
    })
  })

  it('rıza reddedilince oturumun iptal edildiğini söyler', async () => {
    const fetchMock = renderWizard(() => jsonResponse(204))

    await userEvent.click(screen.getByRole('button', { name: 'Rızayı reddet' }))

    expect(await screen.findByText(/oturum iptal edildi/)).toBeInTheDocument()
    expect(JSON.parse(fetchMock.mock.calls[0][1]!.body as string)).toEqual({
      granted: false,
      textVersion: 'kvkk-session-v1',
    })
  })

  it('eşleştirme kodunu ekrana yazmadan ajana gönderir', async () => {
    const fetchMock = renderWizard(
      consentOr((url) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) return jsonResponse(200, { status: 'paired' })
        return jsonResponse(500)
      }),
    )
    await acceptConsent()
    await userEvent.click(screen.getByRole('button', { name: 'Ajanla eşleştir' }))

    expect(await screen.findByRole('button', { name: 'Kamerayı aç' })).toBeInTheDocument()
    expect(screen.queryByText(CODE)).not.toBeInTheDocument()
    const pairCall = fetchMock.mock.calls.find(([url]) => url === AGENT_PAIR_URL)
    expect(JSON.parse(pairCall?.[1]?.body as string)).toEqual({ code: CODE })
  })

  it('ajan bulunamazsa gerekçe ile ajansız devam ettirir', async () => {
    const fetchMock = renderWizard(
      consentOr((url) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) throw new TypeError('Failed to fetch')
        if (url.includes('/continue-without-agent')) return jsonResponse(204)
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
    const skip = fetchMock.mock.calls.find(([url]) => String(url).includes('/continue-without-agent'))
    expect(JSON.parse(skip?.[1]?.body as string)).toEqual({ justification: 'Ajan bu bilgisayarda kurulu değil.' })
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

    renderWizard(
      consentOr((url) => {
        if (url.includes('/agent-code')) return jsonResponse(201, { code: CODE, expiresAt: '2026-11-20T12:00:00Z' })
        if (url === AGENT_PAIR_URL) return jsonResponse(200, { status: 'paired' })
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
