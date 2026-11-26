import { afterEach, describe, expect, it, vi } from 'vitest'
import { fittedSize, PhotoPrepareError, PHOTO_JPEG_QUALITY, PHOTO_MAX_BYTES, prepareReferencePhoto } from './prepareReferencePhoto'

const originalGetContext = HTMLCanvasElement.prototype.getContext
const originalToBlob = HTMLCanvasElement.prototype.toBlob

afterEach(() => {
  vi.unstubAllGlobals()
  HTMLCanvasElement.prototype.getContext = originalGetContext
  HTMLCanvasElement.prototype.toBlob = originalToBlob
})

function stubDecoder(width: number, height: number, encodedBytes: number) {
  const drawImage = vi.fn()
  vi.stubGlobal(
    'createImageBitmap',
    vi.fn(async () => ({ width, height, close() {} })),
  )
  HTMLCanvasElement.prototype.getContext = (() => ({ drawImage })) as unknown as typeof HTMLCanvasElement.prototype.getContext
  let encoded: { width: number; height: number; type?: string; quality?: number } | null = null
  HTMLCanvasElement.prototype.toBlob = function (this: HTMLCanvasElement, callback, type, quality) {
    encoded = { width: this.width, height: this.height, type, quality }
    callback?.(new Blob([new Uint8Array(encodedBytes)], { type: 'image/jpeg' }))
  }
  return {
    drawImage,
    encoded: () => encoded,
  }
}

describe('fittedSize', () => {
  it('uzun kenarı 1600 pikselde tutar', () => {
    expect(fittedSize(4032, 3024)).toEqual({ width: 1600, height: 1200 })
    expect(fittedSize(3024, 4032)).toEqual({ width: 1200, height: 1600 })
  })

  it('sınırın altındaki kareyi olduğu gibi bırakır', () => {
    expect(fittedSize(800, 600)).toEqual({ width: 800, height: 600 })
    expect(fittedSize(1600, 900)).toEqual({ width: 1600, height: 900 })
  })
})

describe('prepareReferencePhoto', () => {
  it('5 MB üstü telefon fotoğrafını JPEG 0,9 ve uzun kenar 1600 olarak küçültür', async () => {
    const canvas = stubDecoder(4032, 3024, 48)
    const source = new File([new Uint8Array(6 * 1024 * 1024)], 'IMG_0001.JPG', { type: 'image/jpeg' })

    const prepared = await prepareReferencePhoto(source)

    expect(prepared).not.toBe(source)
    expect(prepared.type).toBe('image/jpeg')
    expect(prepared.name).toBe('IMG_0001.jpg')
    expect(prepared.size).toBeLessThanOrEqual(PHOTO_MAX_BYTES)
    expect(canvas.encoded()).toEqual({ width: 1600, height: 1200, type: 'image/jpeg', quality: PHOTO_JPEG_QUALITY })
    expect(canvas.drawImage).toHaveBeenCalledOnce()
  })

  it('sınırın altındaki küçük JPEG’i yeniden kodlamaz', async () => {
    const canvas = stubDecoder(800, 600, 48)
    const source = new File(['foto'], 'yuz.jpg', { type: 'image/jpeg' })

    const prepared = await prepareReferencePhoto(source)

    expect(prepared).toBe(source)
    expect(canvas.encoded()).toBeNull()
  })

  it('küçük olsa bile PNG’yi JPEG’e çevirir', async () => {
    stubDecoder(640, 480, 24)
    const source = new File(['png'], 'yuz.png', { type: 'image/png' })

    const prepared = await prepareReferencePhoto(source)

    expect(prepared.type).toBe('image/jpeg')
    expect(prepared.name).toBe('yuz.jpg')
  })

  it('desteklenmeyen türde dosyayı okumadan reddeder', async () => {
    const decode = vi.fn()
    vi.stubGlobal('createImageBitmap', decode)

    await expect(prepareReferencePhoto(new File(['x'], 'not.gif', { type: 'image/gif' }))).rejects.toEqual(
      expect.objectContaining({ name: 'PhotoPrepareError', message: 'Fotoğraf JPEG, PNG veya WebP olmalıdır.' }),
    )
    expect(decode).not.toHaveBeenCalled()
  })

  it('küçültme sonrası hâlâ 5 MB üstündeyse yüklemeye izin vermez', async () => {
    stubDecoder(4032, 3024, PHOTO_MAX_BYTES + 1)

    await expect(
      prepareReferencePhoto(new File([new Uint8Array(8)], 'dev.jpg', { type: 'image/jpeg' })),
    ).rejects.toBeInstanceOf(PhotoPrepareError)
    await expect(
      prepareReferencePhoto(new File([new Uint8Array(8)], 'dev.jpg', { type: 'image/jpeg' })),
    ).rejects.toThrow('Fotoğraf 5 MB’den küçük olmalıdır.')
  })

  it('okunamayan dosyada yükleme hatası verir', async () => {
    vi.stubGlobal('createImageBitmap', vi.fn(async () => Promise.reject(new Error('decode'))))

    await expect(prepareReferencePhoto(new File(['bozuk'], 'bozuk.jpg', { type: 'image/jpeg' }))).rejects.toThrow(
      'Fotoğraf okunamadı. Lütfen başka bir dosya deneyin.',
    )
  })
})
