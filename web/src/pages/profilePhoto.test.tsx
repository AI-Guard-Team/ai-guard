import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { jsonResponse, mockFetch } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'
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

describe('kayıt fotoğrafı', () => {
  it('aday olmayan kullanıcıyı kendi ana sayfasına döndürür', () => {
    signInAs('Reviewer')
    renderApp('/profile/photo')

    expect(screen.getByRole('heading', { name: 'İncelemeler' })).toBeInTheDocument()
  })

  it('rıza olmadan kamerayı açmaz ve fotoğraf göndermez', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Candidate')
    renderApp('/profile/photo')

    expect(screen.getByRole('button', { name: 'Kameradan çek' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Biyometrik rıza olmadan fotoğraf kaydedilemez.')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('onay kutusundan sonra fotoğrafı rıza bilgisiyle gönderir', async () => {
    stubPhotoDecoder(800, 600)
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Candidate')
    renderApp('/profile/photo')

    await userEvent.click(screen.getByRole('checkbox'))
    expect(screen.getByRole('button', { name: 'Kameradan çek' })).toBeEnabled()
    await userEvent.upload(screen.getByLabelText('Fotoğraf'), jpeg())
    expect(await screen.findByRole('img', { name: 'Seçilen kayıt fotoğrafı' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Kayıt fotoğrafı kaydedildi.')
    const init = fetchMock.mock.calls[0][1]!
    expect(fetchMock.mock.calls[0][0]).toBe('/api/me/photo')
    expect(init.method).toBe('POST')
    const body = init.body as FormData
    expect(body.get('consent')).toBe('true')
    expect(body.get('consentTextVersion')).toBe('kvkk-biometric-v1')
    expect(body.get('photo')).toBeInstanceOf(File)
  })

  it('5 MB üstü fotoğrafı küçültüp gönderir', async () => {
    stubPhotoDecoder(4032, 3024, 128)
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Candidate')
    renderApp('/profile/photo')

    await userEvent.click(screen.getByRole('checkbox'))
    await userEvent.upload(screen.getByLabelText('Fotoğraf'), jpeg('telefon.jpg', 6 * 1024 * 1024))
    expect(await screen.findByRole('img', { name: 'Seçilen kayıt fotoğrafı' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Kayıt fotoğrafı kaydedildi.')
    const body = fetchMock.mock.calls[0][1]!.body as FormData
    const photo = body.get('photo') as File
    expect(photo).toBeInstanceOf(File)
    expect(photo.type).toBe('image/jpeg')
    expect(photo.name).toBe('telefon.jpg')
    expect(photo.size).toBeLessThanOrEqual(PHOTO_MAX_BYTES)
    expect(photo.size).toBeLessThan(6 * 1024 * 1024)
  })

  it('biyometrik rızayı geri alır', async () => {
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Candidate')
    renderApp('/profile/photo')

    await userEvent.click(screen.getByRole('button', { name: 'Biyometrik rızayı geri al' }))
    await userEvent.click(screen.getByRole('button', { name: 'Rızayı geri al' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Biyometrik rıza geri alındı.')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/me/biometric-consent')
    expect(fetchMock.mock.calls[0][1]!.method).toBe('DELETE')
  })
})
