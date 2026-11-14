import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import App from '../../App'
import { client } from '../../api/client'
import {
  deferred,
  failResult,
  noContentResult,
  okResult,
  testUser,
} from '../../test/apiResponses'
import { AuthProvider } from './AuthContext'
import LoginPage from './LoginPage'
import { useAuth } from './useAuth'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock
const post = client.POST as unknown as Mock

const WRONG = 'Wrong email or password. Please try again.'
const GENERIC =
  'Something went wrong. Please check your connection and try again.'

function renderLogin(
  entry: string | { pathname: string; state: unknown } = '/login',
) {
  render(
    <MemoryRouter initialEntries={[entry]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function fillAndSubmit(
  user: ReturnType<typeof userEvent.setup>,
  email = 'sara@example.com',
  password = 'Secret1!',
) {
  await user.type(await screen.findByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.click(screen.getByRole('button', { name: 'Log in' }))
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockResolvedValue(failResult(401))
})

describe('LoginPage', () => {
  it('shows the login page with labelled email and password fields inside the layout', async () => {
    renderLogin()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('main')).toContainElement(
      screen.getByLabelText('Email'),
    )
    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email')
    expect(screen.getByLabelText('Password')).toHaveAttribute(
      'type',
      'password',
    )
    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
  })

  it('links to the register page', async () => {
    renderLogin()

    expect(
      await screen.findByRole('link', { name: 'Create an account' }),
    ).toHaveAttribute('href', '/register')
  })

  it('shows Please enter messages and does not call the API when the fields are empty', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.click(await screen.findByRole('button', { name: 'Log in' }))

    expect(screen.getByText('Please enter your email.')).toBeInTheDocument()
    expect(screen.getByText('Please enter your password.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('does not call the API when only the password is empty', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(screen.getByText('Please enter your password.')).toBeInTheDocument()
    expect(screen.queryByText('Please enter your email.')).toBeNull()
    expect(post).not.toHaveBeenCalled()
  })

  it('calls the login endpoint, shows Log out in the header and goes to the home page after a 200', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser))
    renderLogin()

    await fillAndSubmit(user)

    expect(post).toHaveBeenCalledWith('/api/auth/login', {
      body: { email: 'sara@example.com', password: 'Secret1!' },
    })
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Welcome to PawsPost',
      }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
  })

  it('shows the wrong credentials message on a 401 with no status code', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(401, { title: 'Unauthorized', status: 401 }),
    )
    renderLogin()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(WRONG)
    expect(alert).not.toHaveTextContent(/401|unauthorized/i)
    expect(alert.textContent).not.toMatch(/\b(email|password) (is|was)\b/i)
  })

  it('shows the same message for every 401, so a lockout looks identical', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(401, {
        title: 'Locked out',
        detail: 'Try again in 5 minutes',
      }),
    )
    renderLogin()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(WRONG)
    expect(alert).not.toHaveTextContent(/lock|minutes/i)
  })

  it('shows server field errors next to the matching fields on a 400', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(400, {
        errors: {
          Email: ['Email is required.'],
          Password: ['Password is required.'],
        },
      }),
    )
    renderLogin()

    await fillAndSubmit(user)

    expect(await screen.findByText('Email is required.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription(
      'Email is required.',
    )
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription(
      'Password is required.',
    )
  })

  it('shows the generic friendly error when the network is down', async () => {
    const user = userEvent.setup()
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    renderLogin()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(GENERIC)
    expect(alert).not.toHaveTextContent(/failed to fetch|typeerror/i)
  })

  it('shows the generic friendly error on an unexpected status', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(500, { title: 'Server exploded' }))
    renderLogin()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(GENERIC)
    expect(alert).not.toHaveTextContent(/500|exploded/i)
  })

  it('disables the submit button and shows the loading text while submitting', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderLogin()

    await fillAndSubmit(user)

    const button = screen.getByRole('button', { name: 'Please wait...' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByLabelText('Email')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Password')).toHaveAttribute('readonly')

    pending.resolve(okResult(testUser))
    await screen.findByRole('heading', {
      level: 1,
      name: 'Welcome to PawsPost',
    })
  })

  it('sends only one request when the form is submitted twice quickly', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderLogin()
    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!')

    await user.dblClick(screen.getByRole('button', { name: 'Log in' }))

    expect(post).toHaveBeenCalledTimes(1)
    pending.resolve(okResult(testUser))
    await screen.findByRole('heading', {
      level: 1,
      name: 'Welcome to PawsPost',
    })
  })

  it('keeps the email and clears the password after a failed login', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(401))
    renderLogin()

    await fillAndSubmit(user)

    await screen.findByRole('alert')
    expect(screen.getByLabelText('Email')).toHaveValue('sara@example.com')
    expect(screen.getByLabelText('Password')).toHaveValue('')
  })

  it('never shows the password in the page text after a failure', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(400, { errors: { Password: ['Password is not valid.'] } }),
    )
    renderLogin()

    await fillAndSubmit(user, 'sara@example.com', 'Sup3rSecret!')

    await screen.findByText('Password is not valid.')
    expect(screen.getByRole('main')).not.toHaveTextContent('Sup3rSecret!')
  })

  it('never shows the password in the error text for a 401', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(401))
    renderLogin()

    await fillAndSubmit(user, 'sara@example.com', 'Sup3rSecret!')

    expect(await screen.findByRole('alert')).not.toHaveTextContent(
      'Sup3rSecret!',
    )
  })

  it('shows the account ready message when arriving from register', async () => {
    renderLogin({
      pathname: '/login',
      state: { notice: 'Your account is ready. Please log in.' },
    })

    // The notice is filled into the status region after the first render.
    const notice = await screen.findByText(
      'Your account is ready. Please log in.',
    )
    expect(notice).toBe(screen.getByRole('status'))
  })

  it('shows no account ready message when opened directly', async () => {
    renderLogin()

    await screen.findByRole('link', { name: 'Register' })
    expect(screen.queryByText(/account is ready/i)).toBeNull()
  })

  it('keeps an empty status region in the page so a later notice is announced', async () => {
    renderLogin()

    await screen.findByRole('link', { name: 'Register' })
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('works with the keyboard only: tab to the fields and press Enter to submit', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser))
    renderLogin()
    await screen.findByRole('link', { name: 'Register' })

    // Tab order: logo, Log in, Register, then the form.
    await user.tab()
    await user.tab()
    await user.tab()
    await user.tab()
    expect(screen.getByLabelText('Email')).toHaveFocus()
    await user.keyboard('sara@example.com')
    await user.tab()
    expect(screen.getByLabelText('Password')).toHaveFocus()
    await user.keyboard('Secret1!{Enter}')

    expect(post).toHaveBeenCalledWith('/api/auth/login', {
      body: { email: 'sara@example.com', password: 'Secret1!' },
    })
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Welcome to PawsPost',
      }),
    ).toBeInTheDocument()
  })

  it('shows the page as normal without redirect for a logged-in user', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderLogin()

    expect(
      await screen.findByRole('button', { name: 'Log out' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
  })
})

describe('LoginPage focus after a failed submit', () => {
  it('keeps focus in the password field while the request runs', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderLogin()

    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!{Enter}')

    expect(
      screen.getByRole('button', { name: 'Please wait...' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toHaveFocus()

    pending.resolve(okResult(testUser))
    await screen.findByRole('heading', {
      level: 1,
      name: 'Welcome to PawsPost',
    })
  })

  it('moves focus to the email field when both fields are empty', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.click(await screen.findByRole('button', { name: 'Log in' }))

    expect(screen.getByLabelText('Email')).toHaveFocus()
  })

  it('moves focus to the password field when only the password has a server error', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(400, { errors: { Password: ['Password is not valid.'] } }),
    )
    renderLogin()

    await fillAndSubmit(user)

    await screen.findByText('Password is not valid.')
    expect(screen.getByLabelText('Password')).toHaveFocus()
  })

  it('moves focus to the error message when there is no field error', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(401))
    renderLogin()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(document.activeElement).toContainElement(alert)
  })
})

describe('LoginPage when a logout wins over the login', () => {
  // Logs out without navigating, so only LoginPage can change the route.
  function LogoutProbe() {
    const { logout } = useAuth()
    return (
      <button type="button" onClick={() => void logout()}>
        Probe log out
      </button>
    )
  }

  it('stays on the form without an error when a logout finishes during the login', async () => {
    const user = userEvent.setup()
    const loginResponse = deferred<unknown>()
    post.mockImplementation((path: string) =>
      path === '/api/auth/login'
        ? loginResponse.promise
        : Promise.resolve(noContentResult()),
    )
    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <Routes>
            <Route
              path="/login"
              element={
                <>
                  <LoginPage />
                  <LogoutProbe />
                </>
              }
            />
            <Route path="/" element={<p>Home page</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    )

    await fillAndSubmit(user)
    await user.click(screen.getByRole('button', { name: 'Probe log out' }))
    expect(post).toHaveBeenCalledWith('/api/auth/logout')
    loginResponse.resolve(okResult(testUser))

    expect(
      await screen.findByRole('button', { name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('Home page')).toBeNull()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByLabelText('Email')).toHaveValue('sara@example.com')
  })
})
