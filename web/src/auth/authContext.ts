import { createContext } from 'react'
import type { SessionUser } from './jwt'
import type { Tokens } from './tokenStore'

export interface AuthContextValue {
  user: SessionUser | null
  signIn: (tokens: Tokens) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
