import { isRole, type Role } from './roles'

export interface SessionUser {
  id: string
  role: Role
  email?: string
  name?: string
}

// ASP.NET Core, kullandığı JWT işleyicisine göre kısa ("role") ya da uzun URI biçiminde talep adı üretir; ikisi de desteklenir.
const CLAIM = {
  id: ['sub', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
  role: ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
  email: ['email', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
  name: ['name', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'],
}

function pick(payload: Record<string, unknown>, names: string[]): unknown {
  for (const name of names) {
    const value = payload[name]
    if (value !== undefined) return Array.isArray(value) ? value[0] : value
  }
  return undefined
}

function decodeBase64Url(segment: string): string {
  const base64 = segment.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
  const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0))
  // atob tek baytlık karakter döndürür; Türkçe adların bozulmaması için UTF-8 olarak çözülür.
  return new TextDecoder().decode(bytes)
}

// İmza burada doğrulanmaz; doğrulama arka ucun işidir. Önyüz belirteci yalnızca menü ve yönlendirme için okur.
export function decodeAccessToken(token: string): SessionUser | null {
  const parts = token.split('.')
  if (parts.length !== 3) return null

  let payload: Record<string, unknown>
  try {
    payload = JSON.parse(decodeBase64Url(parts[1]))
  } catch {
    return null
  }

  const id = pick(payload, CLAIM.id)
  const role = pick(payload, CLAIM.role)
  if (typeof id !== 'string' || !isRole(role)) return null

  const email = pick(payload, CLAIM.email)
  const name = pick(payload, CLAIM.name)
  return {
    id,
    role,
    email: typeof email === 'string' ? email : undefined,
    name: typeof name === 'string' ? name : undefined,
  }
}
