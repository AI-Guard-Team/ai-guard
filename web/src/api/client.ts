import { tokenStore } from '../auth/tokenStore'
import { getLang } from '../i18n/lang'
import { defineMessages, pick } from '../i18n/messages'

export interface TokenResponse {
  accessToken: string
  refreshToken?: string | null
}

export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, message: string, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }

  /**
   * Sunucunun Türkçe kısa nedeni (ProblemDetails "title"); ASP.NET'in İngilizce varsayılan başlığıysa undefined.
   * Arayüz İngilizceyken de undefined: sunucu metinleri yalnızca Türkçe (K-45).
   */
  get title(): string | undefined {
    return getLang() === 'tr' ? userFacingTitle(this.body) : undefined
  }
}

// ASP.NET'in varsayılan başlıkları ("Bad Request", "One or more validation errors occurred.") yalnızca
// ASCII; bizim uçlarımızın başlıkları Türkçe. Türkçe harf içermeyen başlık kullanıcıya gösterilmez.
const TURKISH_LETTER = /[çğıöşüÇĞİÖŞÜ]/

function userFacingTitle(body: unknown): string | undefined {
  if (!body || typeof body !== 'object') return undefined
  const { title } = body as { title?: unknown }
  return typeof title === 'string' && TURKISH_LETTER.test(title) ? title : undefined
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  /** false ise Authorization başlığı eklenmez ve 401'de yenileme denenmez (giriş, şifre belirleme gibi uçlar). */
  auth?: boolean
  signal?: AbortSignal
}

const messages = defineMessages({
  tr: {
    status: {
      0: 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.',
      400: 'Gönderilen bilgiler geçersiz.',
      401: 'Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.',
      403: 'Bu işlem için yetkiniz yok.',
      404: 'İstenen kayıt bulunamadı.',
      409: 'İşlem mevcut kayıtla çakışıyor.',
      413: 'Gönderilen dosya çok büyük.',
      429: 'Çok fazla deneme yaptınız. Lütfen biraz bekleyip tekrar deneyin.',
    } as Record<number, string>,
    server: 'Sunucuda bir hata oluştu. Lütfen daha sonra tekrar deneyin.',
    unexpected: 'Beklenmeyen bir hata oluştu.',
  },
  en: {
    status: {
      0: 'The server could not be reached. Check your internet connection and try again.',
      400: 'The information you sent is not valid.',
      401: 'Your session has expired. Please sign in again.',
      403: 'You are not allowed to do this.',
      404: 'The requested record was not found.',
      409: 'This action conflicts with the current record.',
      413: 'The file you sent is too large.',
      429: 'Too many attempts. Please wait a moment and try again.',
    },
    server: 'Something went wrong on the server. Please try again later.',
    unexpected: 'An unexpected error occurred.',
  },
})

/** Duruma göre geçerli dildeki genel hata metni. */
export function statusMessage(status: number): string {
  const m = pick(messages)
  return m.status[status] ?? (status >= 500 ? m.server : m.unexpected)
}

function errorMessage(status: number, body: unknown): string {
  // Sunucu metinleri Türkçe; arayüz İngilizceyken duruma göre İngilizce genel metin gösterilir.
  if (getLang() !== 'tr') return statusMessage(status)
  // Önce "detail" (ayrıntı ve ne yapılacağı), yoksa Türkçe "title"; İngilizce varsayılan başlık gösterilmez.
  if (body && typeof body === 'object') {
    const { detail, message } = body as { detail?: unknown; message?: unknown }
    if (typeof detail === 'string' && detail) return detail
    if (typeof message === 'string' && message) return message
    const title = userFacingTitle(body)
    if (title) return title
  }
  return statusMessage(status)
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text) return undefined
  const contentType = response.headers.get('Content-Type') ?? ''
  return contentType.includes('json') ? JSON.parse(text) : text
}

function requestBody(body: unknown): BodyInit | undefined {
  if (body === undefined) return undefined
  // FormData'da Content-Type yazılmaz; tarayıcı sınır bilgisini kendisi ekler.
  if (typeof FormData !== 'undefined' && body instanceof FormData) return body
  return JSON.stringify(body)
}

async function send(path: string, options: RequestOptions, accessToken: string | null): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const body = requestBody(options.body)
  if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  try {
    return await fetch(path, {
      method: options.method ?? 'GET',
      headers,
      body,
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, statusMessage(0))
  }
}

let refreshing: Promise<boolean> | null = null

async function doRefresh(): Promise<boolean> {
  const refreshToken = tokenStore.get()?.refreshToken
  if (!refreshToken) {
    tokenStore.clear()
    return false
  }

  try {
    const response = await send('/api/auth/refresh', { method: 'POST', body: { refreshToken } }, null)
    if (!response.ok) throw new Error()
    const data = (await readBody(response)) as TokenResponse
    tokenStore.set({ accessToken: data.accessToken, refreshToken: data.refreshToken ?? null })
    return true
  } catch {
    // Yenileme başarısızsa oturum kapatılır; RequireAuth kullanıcıyı "oturum süresi doldu" ekranına yönlendirir.
    tokenStore.clear('expired')
    return false
  }
}

/**
 * Aynı anda 401 alan birden fazla istek tek bir yenileme isteğini bekler.
 * Yenileme belirteci tek kullanımlık olduğundan (E2) ikinci bir yenileme isteği reddedilir ve oturum düşerdi.
 */
export function refreshTokens(): Promise<boolean> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null
  })
  return refreshing
}

export async function apiRequest<T = void>(path: string, options: RequestOptions = {}): Promise<T> {
  const useAuth = options.auth ?? true
  let response = await send(path, options, useAuth ? (tokenStore.get()?.accessToken ?? null) : null)

  // Erişim belirteci 15 dakikada dolduğu için 401'de bir kez yenilenip istek tekrarlanır.
  if (response.status === 401 && useAuth && tokenStore.get()) {
    if (await refreshTokens()) {
      response = await send(path, options, tokenStore.get()?.accessToken ?? null)
    }
  }

  const body = await readBody(response)
  if (!response.ok) throw new ApiError(response.status, errorMessage(response.status, body), body)
  return body as T
}
