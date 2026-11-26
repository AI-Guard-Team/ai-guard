import { afterEach, describe, expect, it, vi } from 'vitest'
import { cameraErrorMessage } from './useCamera'

describe('kamera hata metni', () => {
  afterEach(() => vi.unstubAllGlobals())

  const withMedia = () => vi.stubGlobal('navigator', { ...navigator, mediaDevices: { getUserMedia: vi.fn() } })

  it('izin reddinde adres çubuğundaki simgeyi söyler', () => {
    withMedia()
    expect(cameraErrorMessage(new DOMException('x', 'NotAllowedError'))).toMatch(/İzin ver/)
  })

  it('kamera yoksa dosyadan seçmeyi önerir', () => {
    withMedia()
    expect(cameraErrorMessage(new DOMException('x', 'NotFoundError'))).toMatch(/kamera bulunamadı/)
  })

  it('kamera başka uygulamadaysa onu kapatmayı söyler', () => {
    withMedia()
    expect(cameraErrorMessage(new DOMException('x', 'NotReadableError'))).toMatch(/başka bir uygulama/)
  })

  it('tarayıcı kamera arayüzü sunmuyorsa (http) güvenli bağlantıyı söyler', () => {
    vi.stubGlobal('navigator', { ...navigator, mediaDevices: undefined })
    expect(cameraErrorMessage(new Error('x'))).toMatch(/https/)
  })
})
