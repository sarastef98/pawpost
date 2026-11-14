import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../features/auth/AuthContext'
import Layout from './Layout'

// The auth provider asks the API who is logged in. Tests answer "nobody" (401)
// at the API client boundary.
vi.mock('../api/client', () => ({
  client: {
    GET: vi.fn(() =>
      Promise.resolve({
        data: undefined,
        error: {},
        response: { status: 401, ok: false },
      }),
    ),
    POST: vi.fn(),
  },
}))

function renderLayout() {
  render(
    <MemoryRouter initialEntries={['/']}>
      <AuthProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<p>Page content</p>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('Layout', () => {
  it('shows a logo link to the home page in the header', () => {
    renderLayout()

    const logo = screen.getByRole('link', { name: 'PawsPost' })
    expect(screen.getByRole('banner')).toContainElement(logo)
    expect(logo).toHaveAttribute('href', '/')
  })

  it('shows a navigation landmark', () => {
    renderLayout()

    expect(screen.getByRole('navigation')).toBeInTheDocument()
  })

  it('shows the page content in the main area', () => {
    renderLayout()

    expect(screen.getByRole('main')).toHaveTextContent('Page content')
  })

  it('shows PawsPost and a four-digit year in the footer', () => {
    renderLayout()

    const footer = screen.getByRole('contentinfo')
    expect(footer).toHaveTextContent('PawsPost')
    expect(footer).toHaveTextContent(/\b\d{4}\b/)
  })

  it('lets the logo link receive focus with the Tab key', async () => {
    const user = userEvent.setup()
    renderLayout()

    await user.tab()

    expect(screen.getByRole('link', { name: 'PawsPost' })).toHaveFocus()
  })
})
