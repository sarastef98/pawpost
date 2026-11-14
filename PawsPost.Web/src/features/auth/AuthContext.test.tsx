import {
  act,
  render,
  renderHook,
  screen,
  waitFor,
} from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import {
  deferred,
  failResult,
  noContentResult,
  okResult,
  testUser,
} from '../../test/apiResponses'
import { AuthProvider, useAuth } from './AuthContext'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock
const post = client.POST as unknown as Mock

function wrapper({ children }: { children: ReactNode }) {
  return <AuthProvider>{children}</AuthProvider>
}

function renderAuth() {
  return renderHook(() => useAuth(), { wrapper })
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
})

describe('AuthProvider start check', () => {
  it('starts in the loading state', () => {
    get.mockReturnValue(new Promise(() => {}))

    const { result } = renderAuth()

    expect(result.current.status).toBe('loading')
    expect(result.current.user).toBeNull()
  })

  it('calls /me only once on start', async () => {
    get.mockResolvedValue(failResult(401))

    const { result, rerender } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))
    rerender()

    expect(get).toHaveBeenCalledTimes(1)
    expect(get).toHaveBeenCalledWith('/api/auth/me')
  })

  it('is logged in with the user when /me returns 200', async () => {
    get.mockResolvedValue(okResult(testUser))

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('loggedIn'))
    expect(result.current.user).toEqual(testUser)
  })

  it('is logged out when /me returns 401', async () => {
    get.mockResolvedValue(failResult(401))

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('loggedOut'))
    expect(result.current.user).toBeNull()
  })

  it('is logged out when /me fails by network', async () => {
    get.mockRejectedValue(new TypeError('Failed to fetch'))

    const { result } = renderAuth()

    await waitFor(() => expect(result.current.status).toBe('loggedOut'))
    expect(result.current.user).toBeNull()
  })
})

describe('AuthProvider login', () => {
  it('sets the user after a successful login without asking /me again', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockResolvedValue(okResult(testUser))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.login('sara@example.com', 'Secret1!')
    })

    expect(outcome).toEqual({ ok: true })
    expect(post).toHaveBeenCalledWith('/api/auth/login', {
      body: { email: 'sara@example.com', password: 'Secret1!' },
    })
    expect(result.current.status).toBe('loggedIn')
    expect(result.current.user).toEqual(testUser)
    expect(get).toHaveBeenCalledTimes(1)
  })

  it('returns the wrong credentials message and stays logged out on a 401', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockResolvedValue(failResult(401))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.login('a@b.c', 'x')
    })

    expect(outcome).toEqual({
      ok: false,
      errors: { general: 'Wrong email or password. Please try again.' },
    })
    expect(result.current.status).toBe('loggedOut')
  })

  it('returns the generic message when login fails by network', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.login('a@b.c', 'x')
    })

    expect(outcome).toEqual({
      ok: false,
      errors: {
        general:
          'Something went wrong. Please check your connection and try again.',
      },
    })
  })
})

describe('AuthProvider register', () => {
  it('does not change the auth state after a successful register', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockResolvedValue(okResult(testUser, 201))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.register('sara@example.com', 'Secret1!')
    })

    expect(outcome).toEqual({ ok: true })
    expect(post).toHaveBeenCalledWith('/api/auth/register', {
      body: { email: 'sara@example.com', password: 'Secret1!' },
    })
    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })

  it('returns the email taken message on a 409', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockResolvedValue(failResult(409))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.register('a@b.c', 'x')
    })

    expect(outcome).toEqual({
      ok: false,
      errors: {
        general: 'This email is already registered. Try logging in instead.',
        emailTaken: true,
      },
    })
  })

  it('returns the generic message when register fails by network', async () => {
    get.mockResolvedValue(failResult(401))
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedOut'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.register('a@b.c', 'x')
    })

    expect(outcome).toEqual({
      ok: false,
      errors: {
        general:
          'Something went wrong. Please check your connection and try again.',
      },
    })
  })
})

describe('AuthProvider logout', () => {
  async function renderLoggedIn() {
    get.mockResolvedValue(okResult(testUser))
    const view = renderAuth()
    await waitFor(() => expect(view.result.current.status).toBe('loggedIn'))
    return view
  }

  it('clears the user after a successful logout', async () => {
    const { result } = await renderLoggedIn()
    post.mockResolvedValue(noContentResult())

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.logout()
    })

    expect(outcome).toEqual({ ok: true })
    expect(post).toHaveBeenCalledWith('/api/auth/logout')
    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })

  it('counts a 401 from logout as success', async () => {
    const { result } = await renderLoggedIn()
    post.mockResolvedValue(failResult(401))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.logout()
    })

    expect(outcome).toEqual({ ok: true })
    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })

  it('keeps the user logged in when logout fails by network', async () => {
    const { result } = await renderLoggedIn()
    post.mockRejectedValue(new TypeError('Failed to fetch'))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.logout()
    })

    expect(outcome).toEqual({ ok: false })
    expect(result.current.status).toBe('loggedIn')
    expect(result.current.user).toEqual(testUser)
  })

  it('keeps the user logged in when logout returns an unexpected status', async () => {
    const { result } = await renderLoggedIn()
    post.mockResolvedValue(failResult(500))

    let outcome: unknown
    await act(async () => {
      outcome = await result.current.logout()
    })

    expect(outcome).toEqual({ ok: false })
    expect(result.current.status).toBe('loggedIn')
  })
})

describe('AuthProvider late results', () => {
  it('stays logged in when a slow /me 401 arrives after a login', async () => {
    const me = deferred<unknown>()
    get.mockReturnValue(me.promise)
    post.mockResolvedValue(okResult(testUser))
    const { result } = renderAuth()

    await act(async () => {
      await result.current.login('sara@example.com', 'Secret1!')
    })
    await act(async () => {
      me.resolve(failResult(401))
    })

    expect(result.current.status).toBe('loggedIn')
    expect(result.current.user).toEqual(testUser)
  })

  it('stays logged out when a slow /me 200 arrives after a logout', async () => {
    const me = deferred<unknown>()
    get.mockReturnValue(me.promise)
    post.mockResolvedValue(noContentResult())
    const { result } = renderAuth()

    await act(async () => {
      await result.current.logout()
    })
    await act(async () => {
      me.resolve(okResult(testUser))
    })

    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })

  it('still applies a slow /me result after a failed login', async () => {
    const me = deferred<unknown>()
    get.mockReturnValue(me.promise)
    post.mockResolvedValue(failResult(401))
    const { result } = renderAuth()

    await act(async () => {
      await result.current.login('a@b.c', 'x')
    })
    await act(async () => {
      me.resolve(okResult(testUser))
    })

    expect(result.current.status).toBe('loggedIn')
    expect(result.current.user).toEqual(testUser)
  })

  it('stays logged out when a login resolves after a logout', async () => {
    get.mockResolvedValue(okResult(testUser))
    const loginResponse = deferred<unknown>()
    post.mockImplementation((path: string) =>
      path === '/api/auth/login'
        ? loginResponse.promise
        : Promise.resolve(noContentResult()),
    )
    const { result } = renderAuth()
    await waitFor(() => expect(result.current.status).toBe('loggedIn'))

    let loginDone: Promise<unknown> = Promise.resolve()
    act(() => {
      loginDone = result.current.login('sara@example.com', 'Secret1!')
    })
    await act(async () => {
      await result.current.logout()
    })
    await act(async () => {
      loginResponse.resolve(okResult(testUser))
      await loginDone
    })

    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })

  it('ignores the /me result of the first StrictMode mount', async () => {
    const first = deferred<unknown>()
    const second = deferred<unknown>()
    get.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise)
    const { result } = renderHook(() => useAuth(), {
      wrapper,
      reactStrictMode: true,
    })
    // StrictMode mounts, unmounts and mounts again, so the check runs twice.
    expect(get).toHaveBeenCalledTimes(2)

    await act(async () => {
      second.resolve(failResult(401))
    })
    await act(async () => {
      first.resolve(okResult(testUser))
    })

    expect(result.current.status).toBe('loggedOut')
    expect(result.current.user).toBeNull()
  })
})

describe('useAuth', () => {
  it('throws when used outside the provider', () => {
    function Probe() {
      useAuth()
      return <p>probe</p>
    }
    // React logs the thrown error; keep the test output quiet.
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<Probe />)).toThrow(
      'useAuth must be used inside AuthProvider',
    )
    expect(screen.queryByText('probe')).not.toBeInTheDocument()

    spy.mockRestore()
  })
})
