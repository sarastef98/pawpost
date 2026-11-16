import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import Layout from '../../components/Layout'
import {
  deferred,
  failResult,
  noContentResult,
  okResult,
  testUser,
} from '../../test/apiResponses'
import { AuthProvider } from './AuthContext'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock
const post = client.POST as unknown as Mock

function renderHeader(url = '/') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <AuthProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<p>Home page</p>} />
            <Route path="/other" element={<p>Other page</p>} />
            <Route path="/pet-ads/new" element={<p>Post page</p>} />
            <Route path="/login" element={<p>Login page</p>} />
            <Route path="/register" element={<p>Register page</p>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

const LOGOUT_FAILED = 'We could not log you out. Please try again.'

beforeEach(() => {
  get.mockReset()
  post.mockReset()
})

describe('AuthNav', () => {
  it('shows only the loading state and no links or buttons while the start check runs', () => {
    get.mockReturnValue(new Promise(() => {}))

    renderHeader()

    const nav = screen.getByRole('navigation', { name: 'Main' })
    expect(nav).toContainElement(screen.getByRole('status'))
    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Register' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull()
  })

  it('shows Log in and Register links with their addresses when /me returns 401', async () => {
    get.mockResolvedValue(failResult(401))

    renderHeader()

    expect(await screen.findByRole('link', { name: 'Log in' })).toHaveAttribute(
      'href',
      '/login',
    )
    expect(screen.getByRole('link', { name: 'Register' })).toHaveAttribute(
      'href',
      '/register',
    )
    expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull()
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows a Log out button and no links, email or greeting when /me returns 200', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderHeader()

    expect(
      await screen.findByRole('button', { name: 'Log out' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Register' })).toBeNull()
    expect(screen.queryByText(testUser.email)).toBeNull()
    expect(screen.getByRole('banner')).not.toHaveTextContent(/hello|welcome/i)
  })

  it('shows the logged-out links with no error when the start check fails by network', async () => {
    get.mockRejectedValue(new TypeError('Failed to fetch'))

    renderHeader()

    expect(
      await screen.findByRole('link', { name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Register' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('calls the logout endpoint, goes to the home page and shows the logged-out links again', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    post.mockResolvedValue(noContentResult())
    renderHeader('/other')

    await user.click(await screen.findByRole('button', { name: 'Log out' }))

    expect(post).toHaveBeenCalledWith('/api/auth/logout')
    expect(await screen.findByText('Home page')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Log in' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Register' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Log out' })).toBeNull()
  })

  it('shows the logged-out links when logout returns 401', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    post.mockResolvedValue(failResult(401))
    renderHeader('/other')

    await user.click(await screen.findByRole('button', { name: 'Log out' }))

    expect(
      await screen.findByRole('link', { name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('keeps the user logged in and shows a message when logout fails by network', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    renderHeader('/other')

    await user.click(await screen.findByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(LOGOUT_FAILED)
    expect(screen.getByRole('button', { name: 'Log out' })).toBeEnabled()
    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull()
    expect(screen.getByText('Other page')).toBeInTheDocument()
  })

  it('removes the logout failure message after a later logout succeeds', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    post.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    post.mockResolvedValueOnce(noContentResult())
    renderHeader('/other')

    await user.click(await screen.findByRole('button', { name: 'Log out' }))
    await screen.findByRole('alert')
    await user.click(screen.getByRole('button', { name: 'Log out' }))

    expect(
      await screen.findByRole('link', { name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(LOGOUT_FAILED)).toBeNull()
  })

  it('sends only one logout request when Log out is pressed twice quickly', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof noContentResult>>()
    get.mockResolvedValue(okResult(testUser))
    post.mockReturnValue(pending.promise)
    renderHeader('/other')

    const button = await screen.findByRole('button', { name: 'Log out' })
    await user.dblClick(button)
    pending.resolve(noContentResult())

    await screen.findByRole('link', { name: 'Log in' })
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('lets the logged-out links be reached with the Tab key', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(failResult(401))
    renderHeader()
    await screen.findByRole('link', { name: 'Log in' })

    await user.tab()
    expect(screen.getByRole('link', { name: 'PawsPost' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('link', { name: 'Register' })).toHaveFocus()
  })

  it('lets the Log out button be reached with the Tab key', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    renderHeader()
    await screen.findByRole('button', { name: 'Log out' })

    await user.tab()
    expect(screen.getByRole('link', { name: 'PawsPost' })).toHaveFocus()
    await user.tab()
    expect(
      screen.getByRole('link', { name: 'Ask the assistant' }),
    ).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('link', { name: 'Post an ad' })).toHaveFocus()
    await user.tab()

    expect(screen.getByRole('button', { name: 'Log out' })).toHaveFocus()
  })

  it('does not redirect a logged-in user who opens the login page', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderHeader('/login')

    expect(
      await screen.findByRole('button', { name: 'Log out' }),
    ).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.getByText('Login page')).toBeInTheDocument(),
    )
  })

  it('does not redirect a logged-in user who opens the register page', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderHeader('/register')

    expect(
      await screen.findByRole('button', { name: 'Log out' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Register page')).toBeInTheDocument()
  })
})

describe('AuthNav post an ad link', () => {
  it('links to the create page when logged in', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderHeader()

    expect(
      await screen.findByRole('link', { name: 'Post an ad' }),
    ).toHaveAttribute('href', '/pet-ads/new')
  })

  it('opens the create page when clicked', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(testUser))
    renderHeader()

    await user.click(await screen.findByRole('link', { name: 'Post an ad' }))

    expect(screen.getByText('Post page')).toBeInTheDocument()
  })

  it('is not shown when logged out', async () => {
    get.mockResolvedValue(failResult(401))

    renderHeader()

    await screen.findByRole('link', { name: 'Log in' })
    expect(screen.queryByRole('link', { name: 'Post an ad' })).toBeNull()
  })

  it('is not shown while the start check runs', () => {
    get.mockReturnValue(new Promise(() => {}))

    renderHeader()

    expect(screen.queryByRole('link', { name: 'Post an ad' })).toBeNull()
  })
})
