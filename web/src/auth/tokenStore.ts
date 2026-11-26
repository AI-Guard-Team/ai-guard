export interface Tokens {
  accessToken: string
  refreshToken: string | null
}

/** Oturum neden kapandı: "expired" belirteç yenilenemedi, "signout" kullanıcı çıktı. */
export type EndReason = 'expired' | 'signout'

// Belirteçler sekmeye özel sessionStorage'da (K-44): sayfa yenilenince oturum düşmez, sekme kapanınca silinir.
// localStorage kullanılmaz (sekmeler arası ve kalıcı olurdu). XSS'e karşı CSP script-src 'self' (K-35).
// httpOnly çerez kararı verilirse değişmesi gereken tek yer bu dosyadır.
const STORAGE_KEY = 'aiguard.session'

function load(): Tokens | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<Tokens>
    return typeof parsed.accessToken === 'string' ? { accessToken: parsed.accessToken, refreshToken: parsed.refreshToken ?? null } : null
  } catch {
    return null
  }
}

function save(tokens: Tokens | null): void {
  try {
    if (tokens) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(tokens))
    else sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Depolama kapalıysa oturum yalnızca bellekte; yenilemede düşer.
  }
}

let current: Tokens | null = load()
let endReason: EndReason | null = null
const listeners = new Set<() => void>()

export const tokenStore = {
  get(): Tokens | null {
    return current
  },
  set(next: Tokens | null): void {
    current = next
    save(next)
    if (next) endReason = null
    listeners.forEach((listener) => listener())
  },
  /** Oturumu kapatır. Neden yalnızca açık bir oturum kapanırken yazılır. */
  clear(reason?: EndReason): void {
    if (current && reason) endReason = reason
    tokenStore.set(null)
  },
  /** Son oturumun kapanma nedeni; yeni giriş yapılınca sıfırlanır. */
  endReason(): EndReason | null {
    return endReason
  },
  subscribe(listener: () => void): () => void {
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  },
}
