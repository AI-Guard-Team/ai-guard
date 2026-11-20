import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { jsonResponse, mockFetch } from '../test/fetch'
import { renderApp, signInAs } from '../test/renderApp'

afterEach(() => {
  vi.unstubAllGlobals()
})

function jpeg(name = 'yuz.jpg') {
  return new File(['foto'], name, { type: 'image/jpeg' })
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
    const fetchMock = mockFetch(() => jsonResponse(204))
    signInAs('Candidate')
    renderApp('/profile/photo')

    await userEvent.click(screen.getByRole('checkbox'))
    expect(screen.getByRole('button', { name: 'Kameradan çek' })).toBeEnabled()
    await userEvent.upload(screen.getByLabelText('Fotoğraf'), jpeg())
    await userEvent.click(screen.getByRole('button', { name: 'Fotoğrafı kaydet' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Kayıt fotoğrafı kaydedildi.')
    const init = fetchMock.mock.calls[0][1]!
    expect(fetchMock.mock.calls[0][0]).toBe('/api/me/photo')
    expect(init.method).toBe('POST')
    const body = init.body as FormData
    expect(body.get('biometricConsent')).toBe('true')
    expect(body.get('textVersion')).toBe('kvkk-biometric-v1')
    expect(body.get('photo')).toBeInstanceOf(File)
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
