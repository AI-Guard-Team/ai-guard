export interface Tokens {
  accessToken: string
  refreshToken: string | null
}

// Belirteçler XSS ile çalınmasın diye localStorage yerine yalnızca bellekte tutulur.
// httpOnly çerez kararı verilirse değişmesi gereken tek yer bu dosyadır.
let current: Tokens | null = null
const listeners = new Set<() => void>()

export const tokenStore = {
  get(): Tokens | null {
    return current
  },
  set(next: Tokens | null): void {
    current = next
    listeners.forEach((listener) => listener())
  },
  clear(): void {
    tokenStore.set(null)
  },
  subscribe(listener: () => void): () => void {
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  },
}
