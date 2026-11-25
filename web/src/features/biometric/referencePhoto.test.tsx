import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CONSENT_TEXTS, withCandidateBasics } from '../../test/candidateServer'
import { jsonResponse, mockFetch, type FetchHandler } from '../../test/fetch'
import { renderApp, signInAs } from '../../test/renderApp'
import { PHOTO_MAX_BYTES } from './prepareReferencePhoto'

const originalGetContext = HTMLCanvasElement.prototype.getContext
const originalToBlob = HTMLCanvasElement.prototype.toBlob

afterEach(() => {
  vi.unstubAllGlobals()
  HTMLCanvasElement.prototype.getContext = originalGetContext
  HTMLCanvasElement.prototype.toBlob = originalToBlob
})

function jpeg(name = 'yuz.jpg', bytes = 4) {
  return new File([new Uint8Array(bytes)], name, { type: 'image/jpeg' })
}

/** jsdom görüntü çözemediği için küçültme adımı sahte bir bitmap ile geçilir. */
function stubPhotoDecoder(width: number, height: number, encodedBytes = 32) {
  vi.stubGlobal('createImageBitmap', vi.fn(async () => ({ width, height, close() {} })))
  HTMLCanvasElement.prototype.getContext = (() => ({ drawImage() {} })) as unknown as typeof HTMLCanvasElement.prototype.getContext
  HTMLCanvasElement.prototype.toBlob = (callback) => {
    callback?.(new Blob([new Uint8Array(encodedBytes)], { type: 'image/jpeg' }))
  }
}

async function open(handler: FetchHandler, enrolled = false) {
  const fetchMock = mockFetch(withCandidateBasics(handler, { enrolled }))
  signInAs('Candidate')
  await renderApp('/profile/photo')
  await screen.findByText(CONSENT_TEXTS.Biometric.title)
  return fetchMock
}

const photoCall = (fetchMock: ReturnType<typeof mockFetch>) => fetchMock.mock.calls.find(([url]) => url === '/api/me/photo')

describe('kayıt fotoğrafı ve biyometrik rıza', () => {
  it('aday olmayan kullanıcıya 403 gösterir', async () => {
    signInAs('Reviewer')
    await renderApp('/profile/photo')

    expect(screen.getByRole('heading', { name: 'Bu sayfaya erişim yetkiniz yok' })).toBeInTheDocument()
  })

  it('sunucudaki rıza metnini düz yazı olarak gösterir', async () => {
    await open(() => jsonResponse(500))

    expect(screen.getByText('Birinci paragraf.')).toBeInTheDocument()
    expect(screen.getByText('İkinci paragraf.')).toBeInTheDocument()
    expect(screen.getByText(/Metin sürümü kvkk-biometric-v2/)).toBeInTheDocument()
  })

  it('rıza olmadan dosya seçimi ve kamera kapalı, istek gitmez', async () => {
    const fetchMock = await open(() => jsonResponse(204))

    expect(screen.getByRole('button', { name: 'Kameradan çek' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Dosya seç' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Fotoğrafı kaydet' })).toBeDisabled()
    expect(photoCall(fetchMock)).toBeUndefined()
  })

  it('fotoğrafı sunucunun beklediği alanlarla ve ekrandaki metnin sürümüyle gönderir', async () => {
    stubPhotoDecoder(800, 600)
    const fetchMock = await open(() => jsonResponse(204))

    await userEvent.click(screen.getByRole('checkbox'))
    await userEvent.upload(screen.getByLabelText('Fotoğraf dosyası seç'), jpeg())
    expect(await screen.findByRole('img', { name: 'Seçilen kayıt fotoğrafı' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Yüz şablonunuz hazır.')
    const init = photoCall(fetchMock)![1]!
    expect(init.method).toBe('POST')
    const body = init.body as FormData
    // Sunucu: PhotoUploadForm { Photo, Consent, ConsentTextVersion } (A3).
    expect(body.get('consent')).toBe('true')
    expect(body.get('consentTextVersion')).toBe(CONSENT_TEXTS.Biometric.version)
    expect(body.get('photo')).toBeInstanceOf(File)
  })

  it('5 MB üstü fotoğrafı küçültüp gönderir', async () => {
    stubPhotoDecoder(4032, 3024, 128)
    const fetchMock = await open(() => jsonResponse(204))

    await userEvent.click(screen.getByRole('checkbox'))
    await userEvent.upload(screen.getByLabelText('Fotoğraf dosyası seç'), jpeg('telefon.jpg', 6 * 1024 * 1024))
    expect(await screen.findByRole('img', { name: 'Seçilen kayıt fotoğrafı' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    await screen.findByRole('status')
    const photo = (photoCall(fetchMock)![1]!.body as FormData).get('photo') as File
    expect(photo.type).toBe('image/jpeg')
    expect(photo.name).toBe('telefon.jpg')
    expect(photo.size).toBeLessThanOrEqual(PHOTO_MAX_BYTES)
  })

  it('yüz bulunamazsa sunucunun nedenini ve ne yapılacağını gösterir', async () => {
    stubPhotoDecoder(800, 600)
    await open((url) =>
      url === '/api/me/photo' ? jsonResponse(422, { title: 'Fotoğrafta tek ve net bir yüz bulunamadı.', detail: 'Yüzünüzün net göründüğü başka bir fotoğraf yükleyin.' }) : jsonResponse(500),
    )

    await userEvent.click(screen.getByRole('checkbox'))
    await userEvent.upload(screen.getByLabelText('Fotoğraf dosyası seç'), jpeg())
    await screen.findByRole('img', { name: 'Seçilen kayıt fotoğrafı' })
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Fotoğrafta tek ve net bir yüz bulunamadı.')
    expect(alert).toHaveTextContent('başka bir fotoğraf yükleyin')
  })

  it('şablon varsa tarihini gösterir ve rıza geri alınabilir', async () => {
    const fetchMock = await open((url, init) => (url === '/api/me/biometric-consent' && init.method === 'DELETE' ? jsonResponse(204) : jsonResponse(500)), true)

    expect(await screen.findByText('Yüz şablonunuz hazır')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Fotoğrafı güncelle' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Rızamı geri al' }))
    const dialog = screen.getByRole('dialog')
    await userEvent.click(dialog.querySelector('.button-danger') as HTMLElement)

    expect(await screen.findByRole('status')).toHaveTextContent('Biyometrik rızanız geri alındı')
    expect(fetchMock.mock.calls.some(([url, init]) => url === '/api/me/biometric-consent' && init?.method === 'DELETE')).toBe(true)
  })

  it('şablon yokken rıza geri alma bölümü görünmez', async () => {
    await open(() => jsonResponse(500))

    expect(screen.queryByRole('button', { name: 'Rızamı geri al' })).not.toBeInTheDocument()
  })
})
