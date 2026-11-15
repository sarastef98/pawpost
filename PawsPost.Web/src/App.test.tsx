import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import App from './App'
import { AuthProvider } from './features/auth/AuthContext'

// The auth provider asks the API who is logged in. Tests answer "nobody" (401)
// at the API client boundary. The home page asks for the ads list and gets an
// empty one.
vi.mock('./api/client', () => ({
  client: {
    GET: vi.fn((path: string) =>
      Promise.resolve(
        path === '/api/pet-ads'
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
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })

  it('does not show the not-found page at the style guide route', () => {
    renderAt('/styleguide')

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
