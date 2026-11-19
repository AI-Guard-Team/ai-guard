function encodeBase64Url(text: string): string {
  const bytes = new TextEncoder().encode(text)
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

export function makeAccessToken(payload: Record<string, unknown>): string {
  return [encodeBase64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' })), encodeBase64Url(JSON.stringify(payload)), 'imza'].join('.')
}
