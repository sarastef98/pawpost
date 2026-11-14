import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import { client } from '../../api/client'
import type { components } from '../../api/schema'
import { mapFormErrors, networkFormErrors } from './authErrors'
import type { FormErrors } from './authErrors'

type User = components['schemas']['UserResponse']

type AuthStatus = 'loading' | 'loggedOut' | 'loggedIn'

export type ActionResult = { ok: true } | { ok: false; errors: FormErrors }

export type LogoutResult = { ok: true } | { ok: false }

type AuthContextValue = {
  status: AuthStatus
  user: User | null
  login: (email: string, password: string) => Promise<ActionResult>
  register: (email: string, password: string) => Promise<ActionResult>
  logout: () => Promise<LogoutResult>
}

const AuthContext = createContext<AuthContextValue | null>(null)

// The login state lives only in memory. The login cookie is HttpOnly: the
// browser sends it by itself, this code never reads or writes it. The password
// is only passed through to the API and never kept.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<User | null>(null)
  // Counts how often a login or logout has decided the session. A request
  // that started before a newer decision must not overwrite it.
  const version = useRef(0)

  useEffect(() => {
    // Set by the cleanup, so a result that arrives after unmount (or after
    // the extra StrictMode unmount in development) is ignored.
    let cancelled = false
    const startVersion = version.current

    function apply(nextUser: User | null) {
      if (cancelled || version.current !== startVersion) {
        return
      }
      setUser(nextUser)
      setStatus(nextUser ? 'loggedIn' : 'loggedOut')
    }

    async function checkSession() {
      try {
        const { data } = await client.GET('/api/auth/me')
        // No data means 401, which is normal here: nobody is logged in.
        apply(data ?? null)
      } catch {
        // Network failure: treat as logged out, no error shown.
        apply(null)
      }
    }

    void checkSession()
    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(
    async (email: string, password: string): Promise<ActionResult> => {
      const startVersion = version.current
      try {
        const { data, error, response } = await client.POST('/api/auth/login', {
          body: { email, password },
        })
        if (data) {
          // A logout that finished while this login ran is newer and wins.
          if (version.current === startVersion) {
            version.current += 1
            setUser(data)
            setStatus('loggedIn')
          }
          return { ok: true }
        }
        return {
          ok: false,
          errors: mapFormErrors('login', response.status, error),
        }
      } catch {
        return { ok: false, errors: networkFormErrors() }
      }
    },
    [],
  )

  const register = useCallback(
    async (email: string, password: string): Promise<ActionResult> => {
      try {
        const { data, error, response } = await client.POST(
          '/api/auth/register',
          { body: { email, password } },
        )
        if (data) {
          return { ok: true }
        }
        return {
          ok: false,
          errors: mapFormErrors('register', response.status, error),
        }
      } catch {
        return { ok: false, errors: networkFormErrors() }
      }
    },
    [],
  )

  const logout = useCallback(async (): Promise<LogoutResult> => {
    const startVersion = version.current
    try {
      const { response } = await client.POST('/api/auth/logout')
      // 401 means already logged out, so it counts as success.
      if (response.ok || response.status === 401) {
        // A login that finished while this logout ran is newer and wins.
        if (version.current === startVersion) {
          version.current += 1
          setUser(null)
          setStatus('loggedOut')
        }
        return { ok: true }
      }
      return { ok: false }
    } catch {
      return { ok: false }
    }
  }, [])

  const value = useMemo(
    () => ({ status, user, login, register, logout }),
    [status, user, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// oxlint-disable-next-line react/only-export-components
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider')
  }
  return value
}
