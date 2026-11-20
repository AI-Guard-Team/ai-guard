import { useCallback, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './authContext'
import { decodeAccessToken } from './jwt'
import { tokenStore, type Tokens } from './tokenStore'

export function AuthProvider({ children }: { children: ReactNode }) {
  // API istemcisi belirteci arka planda yenilediğinde de arayüzün güncellenmesi için depoya abone olunur.
  const tokens = useSyncExternalStore(tokenStore.subscribe, tokenStore.get)
  const accessToken = tokens?.accessToken ?? null

  const user = useMemo(() => (accessToken ? decodeAccessToken(accessToken) : null), [accessToken])

  const signIn = useCallback((next: Tokens) => tokenStore.set(next), [])

  const value = useMemo<AuthContextValue>(() => ({ user, signIn }), [user, signIn])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
