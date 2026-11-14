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
  const started = useRef(false)

  useEffect(() => {
    // Guard so the start check runs once, also under StrictMode.
    if (started.current) {
      return
    }
    started.current = true

    async function checkSession() {
      try {
        const { data } = await client.GET('/api/auth/me')
        if (data) {
          setUser(data)
          setStatus('loggedIn')
        } else {
          // 401 is normal here: nobody is logged in.
          setStatus('loggedOut')
        }
      } catch {
        // Network failure: treat as logged out, no error shown.
        setStatus('loggedOut')
      }
    }

    void checkSession()
  }, [])

  const login = useCallback(
    async (email: string, password: string): Promise<ActionResult> => {
      try {
        const { data, error, response } = await client.POST('/api/auth/login', {
          body: { email, password },
        })
        if (data) {
          setUser(data)
          setStatus('loggedIn')
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
    try {
      const { response } = await client.POST('/api/auth/logout')
      // 401 means already logged out, so it counts as success.
      if (response.ok || response.status === 401) {
        setUser(null)
        setStatus('loggedOut')
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
