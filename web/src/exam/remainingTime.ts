/** Kalan süre `endsAt − (serverTime + yerel geçen süre)`. İstemci saati kullanılmaz (K-24). */
export function remainingMs(endsAt: string, serverTime: string, elapsedMs: number): number {
  return Date.parse(endsAt) - Date.parse(serverTime) - elapsedMs
}

export function formatRemaining(ms: number): string {
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}
