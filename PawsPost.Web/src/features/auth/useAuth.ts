import { createContext, useContext } from 'react'
import type { components } from '../../api/schema'
import type { FormErrors } from './authErrors'

export type User = components['schemas']['UserResponse']

export type AuthStatus = 'loading' | 'loggedOut' | 'loggedIn'

export type ActionResult = { ok: true } | { ok: false; errors: FormErrors }

export type LogoutResult = { ok: true } | { ok: false }

export type AuthContextValue = {
  status: AuthStatus
  user: User | null
  login: (email: string, password: string) => Promise<ActionResult>
  register: (email: string, password: string) => Promise<ActionResult>
  logout: () => Promise<LogoutResult>
}

// Filled by AuthProvider in AuthContext.tsx. Kept here, apart from the
// provider component, so that file exports only a component.
export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider')
  }
  return value
}
