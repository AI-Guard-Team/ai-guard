// API kayıt fotoğrafını 5 MB ile sınırlıyor. Telefonun orijinal karesi (özellikle iPhone JPEG'i)
// bu sınırı aşabildiği için dosya, yüklenmeden önce uzun kenarı 1600 px olan JPEG'e indiriliyor.

export const ACCEPTED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp']
export const PHOTO_MAX_BYTES = 5 * 1024 * 1024
export const PHOTO_MAX_EDGE = 1600
export const PHOTO_JPEG_QUALITY = 0.9

const TYPE_ERROR = 'Fotoğraf JPEG, PNG veya WebP olmalıdır.'
const SIZE_ERROR = 'Fotoğraf 5 MB’den küçük olmalıdır.'
const READ_ERROR = 'Fotoğraf okunamadı. Lütfen başka bir dosya deneyin.'
const PROCESS_ERROR = 'Fotoğraf işlenemedi. Lütfen başka bir dosya deneyin.'

export class PhotoPrepareError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'PhotoPrepareError'
  }
}

export function rejectedPhotoType(file: File): string | null {
  return ACCEPTED_PHOTO_TYPES.includes(file.type) ? null : TYPE_ERROR
}

/** Uzun kenar `maxEdge` pikselin altına inecek şekilde en-boy oranını korur. */
export function fittedSize(width: number, height: number, maxEdge = PHOTO_MAX_EDGE): { width: number; height: number } {
  if (width < 1 || height < 1) throw new PhotoPrepareError(READ_ERROR)
  const longEdge = Math.max(width, height)
  if (longEdge <= maxEdge) return { width, height }
  const scale = maxEdge / longEdge
  return {
    width: Math.max(1, Math.round(width * scale)),
    height: Math.max(1, Math.round(height * scale)),
  }
}

type DecodedPhoto = {
  width: number
  height: number
  source: CanvasImageSource
  close: () => void
}

async function decode(file: File): Promise<DecodedPhoto> {
  if (typeof createImageBitmap !== 'function') return decodeWithImage(file)
  try {
    // from-image: iPhone JPEG'indeki EXIF yönü uygulanır, portre kare yan yatmaz.
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
    return {
      width: bitmap.width,
      height: bitmap.height,
      source: bitmap,
      close: () => bitmap.close(),
    }
  } catch {
    throw new PhotoPrepareError(READ_ERROR)
  }
}

function decodeWithImage(file: File): Promise<DecodedPhoto> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()
    image.onload = () => {
      URL.revokeObjectURL(url)
      resolve({
        width: image.naturalWidth,
        height: image.naturalHeight,
        source: image,
        close: () => {},
      })
    }
    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new PhotoPrepareError(READ_ERROR))
    }
    image.src = url
  })
}

function canvasBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (!blob) reject(new PhotoPrepareError(PROCESS_ERROR))
        else resolve(blob)
      },
      'image/jpeg',
      PHOTO_JPEG_QUALITY,
    )
  })
}

async function encodeJpeg(source: CanvasImageSource, width: number, height: number): Promise<Blob> {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')
  if (!context) throw new PhotoPrepareError(PROCESS_ERROR)
  context.imageSmoothingEnabled = true
  context.imageSmoothingQuality = 'high'
  context.drawImage(source, 0, 0, width, height)
  return canvasBlob(canvas)
}

function jpegName(name: string): string {
  const base = name.replace(/\.[^.]+$/, '').trim()
  return `${base || 'kayit-fotografi'}.jpg`
}

export async function prepareReferencePhoto(file: File): Promise<File> {
  const typeError = rejectedPhotoType(file)
  if (typeError) throw new PhotoPrepareError(typeError)

  const decoded = await decode(file)
  try {
    const size = fittedSize(decoded.width, decoded.height)
    const alreadyFits =
      file.type === 'image/jpeg' &&
      file.size > 0 &&
      file.size <= PHOTO_MAX_BYTES &&
      size.width === decoded.width &&
      size.height === decoded.height
    if (alreadyFits) return file

    const blob = await encodeJpeg(decoded.source, size.width, size.height)
    if (blob.size > PHOTO_MAX_BYTES) throw new PhotoPrepareError(SIZE_ERROR)
    return new File([blob], jpegName(file.name), { type: 'image/jpeg', lastModified: file.lastModified })
  } finally {
    decoded.close()
  }
}
