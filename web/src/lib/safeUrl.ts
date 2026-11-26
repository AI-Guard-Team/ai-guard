/** Yalnızca https bağlantı (K-49): adayın tıklayacağı adreste `javascript:`, `data:` ve düz http reddedilir. */
export function isHttpsUrl(value: string | null | undefined): value is string {
  if (!value) return false
  try {
    const url = new URL(value)
    return url.protocol === 'https:' && url.hostname.length > 0
  } catch {
    return false
  }
}
