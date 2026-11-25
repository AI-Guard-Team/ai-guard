export interface Tokens {
  accessToken: string
  refreshToken: string | null
}

/** Oturum neden kapandı: "expired" belirteç yenilenemedi, "signout" kullanıcı çıktı. */
export type EndReason = 'expired' | 'signout'

// Belirteçler XSS ile çalınmasın diye localStorage yerine yalnızca bellekte tutulur.
// httpOnly çerez kararı verilirse değişmesi gereken tek yer bu dosyadır.
let current: Tokens | null = null
let endReason: EndReason | null = null
const listeners = new Set<() => void>()

export const tokenStore = {
  get(): Tokens | null {
    return current
  },
  set(next: Tokens | null): void {
    current = next
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
