import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import App from './App'
import { AuthProvider } from './features/auth/AuthContext'
import { AuthContext } from './features/auth/useAuth'

// The auth provider asks the API who is logged in. Tests answer "nobody" (401)
// at the API client boundary. The home page asks for the ads list and gets an
// empty one.
vi.mock('./api/client', () => ({
  client: {
    GET: vi.fn((path: string) =>
      Promise.resolve(
        path === '/api/creators/{id}'
          ? {
              data: {
                id: 'creator-1',
                displayName: 'Anna',
                ads: { items: [], totalCount: 0, page: 1, pageSize: 20 },
              },
              error: undefined,
              response: { status: 200, ok: true },
            }
          : path === '/api/pet-ads'
            ? {
                data: { items: [], totalCount: 0, page: 1, pageSize: 20 },
                error: undefined,
                response: { status: 200, ok: true },
              }
            : {
                data: undefined,
                error: {},
                response: { status: 401, ok: false },
              },
      ),
    ),
    POST: vi.fn(),
  },
}))

function renderAt(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('App', () => {
  it('shows the home page inside the layout at the home route', () => {
    renderAt('/')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
  })

  it('shows the style guide inside the layout at the style guide route', () => {
    renderAt('/styleguide')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Style guide' }),
    ).toBeInTheDocument()
  })

  it('shows the login page inside the layout at the login route', async () => {
    renderAt('/login')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
  })

  it('shows the register page inside the layout at the register route', async () => {
    renderAt('/register')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Create your account',
      }),
    ).toBeInTheDocument()
  })

  it('shows the ad details page inside the layout at the details route', async () => {
    renderAt('/pet-ads/ad-1')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    // The mocked API answers "nobody is logged in", so the guard sends the
    // visitor on to the login page, which proves the route reached the page.
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
  })

  it('goes from the style guide back to the home page when the logo is clicked', async () => {
    const user = userEvent.setup()
    renderAt('/styleguide')

    await user.click(screen.getByRole('link', { name: 'PawsPost' }))

    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
  })
})

describe('App not-found route', () => {
  it('shows the not-found page inside the layout for an unknown URL', () => {
    renderAt('/nope')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      within(screen.getByRole('main')).getByRole('heading', {
        level: 1,
        name: 'Page not found',
      }),
    ).toBeInTheDocument()
  })

  it('shows the not-found page for a deeper unknown URL', () => {
    renderAt('/pet-ads/abc/extra')

    expect(
      screen.getByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeInTheDocument()
  })

  it('goes to the home page when Back to home is clicked', async () => {
    const user = userEvent.setup()
    renderAt('/nope')

    await user.click(screen.getByRole('link', { name: 'Back to home' }))

    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
  })

  it('does not show the not-found page at the home route', () => {
    renderAt('/')

    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })

  it('does not show the not-found page at the style guide route', () => {
    renderAt('/styleguide')

    expect(
      screen.getByRole('heading', { level: 1, name: 'Style guide' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })

  it('does not show the not-found page at the login route', async () => {
    renderAt('/login')

    await screen.findByRole('heading', { level: 1, name: 'Log in' })
    expect(
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })

  it('does not show the not-found page at the register route', async () => {
    renderAt('/register')

    await screen.findByRole('heading', {
      level: 1,
      name: 'Create your account',
    })
    expect(
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })
})

describe('App create ad route', () => {
  it('shows the create page, not the details page, at the new ad route', () => {
    const value = {
      status: 'loggedIn' as const,
      user: { id: 'u1', email: 'sara@example.com' },
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
    }
    render(
      <MemoryRouter initialEntries={['/pet-ads/new']}>
        <AuthContext.Provider value={value}>
          <App />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(
      screen.getByRole('heading', { level: 1, name: 'Post an ad' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeInTheDocument()
  })
})

describe('App assistant route', () => {
  it('shows the assistant page inside the layout at the assistant route', () => {
    const value = {
      status: 'loggedIn' as const,
      user: { id: 'u1', email: 'sara@example.com' },
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
    }
    render(
      <MemoryRouter initialEntries={['/assistant']}>
        <AuthContext.Provider value={value}>
          <App />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Find a pet with the assistant',
      }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Your question')).toBeInTheDocument()
  })

  it('shows the public creator profile inside the layout without login', async () => {
    renderAt('/creators/creator-1')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Anna' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Log in' }),
    ).not.toBeInTheDocument()
  })
})
