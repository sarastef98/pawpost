import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import App from '../../App'
import { client } from '../../api/client'
import {
  deferred,
  failResult,
  okResult,
  testUser,
} from '../../test/apiResponses'
import { AuthProvider } from './AuthContext'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock
const post = client.POST as unknown as Mock

const GENERIC =
  'Something went wrong. Please check your connection and try again.'
const TAKEN = 'This email is already registered. Try logging in instead.'

function renderRegister() {
  render(
    <MemoryRouter initialEntries={['/register']}>
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
  await user.type(screen.getByLabelText('Public name'), 'Sara Test')
  await user.click(screen.getByRole('button', { name: 'Create account' }))
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockResolvedValue(failResult(401))
})

describe('RegisterPage', () => {
  it('shows the register page with labelled email and password fields inside the layout', async () => {
    renderRegister()

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Create your account',
      }),
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

  it('has no repeat password field', async () => {
    renderRegister()

    await screen.findByLabelText('Email')
    expect(screen.getAllByLabelText(/password/i)).toHaveLength(1)
    expect(screen.queryByLabelText(/repeat|confirm/i)).toBeNull()
  })

  it('shows no password rules text', async () => {
    renderRegister()

    await screen.findByLabelText('Email')
    expect(screen.getByLabelText('Password')).not.toHaveAccessibleDescription()
  })

  it('links to the login page', async () => {
    renderRegister()

    const links = await screen.findAllByRole('link', { name: 'Log in' })
    for (const link of links) {
      expect(link).toHaveAttribute('href', '/login')
    }
    expect(screen.getByRole('main')).toHaveTextContent(
      'Already have an account?',
    )
  })

  it('shows Please enter messages and does not call the API when the fields are empty', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.click(
      await screen.findByRole('button', { name: 'Create account' }),
    )

    expect(screen.getByText('Please enter your email.')).toBeInTheDocument()
    expect(screen.getByText('Please enter your password.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('calls the register endpoint and after a 201 goes to the login page with the account ready message', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser, 201))
    renderRegister()

    await fillAndSubmit(user)

    expect(post).toHaveBeenCalledWith('/api/auth/register', {
      body: {
        email: 'sara@example.com',
        password: 'Secret1!',
        displayName: 'Sara Test',
      },
    })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
    // The notice is filled into the status region after the first render.
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'Your account is ready. Please log in.',
      ),
    )
    expect(
      screen.queryByRole('button', { name: 'Log out' }),
    ).not.toBeInTheDocument()
  })

  it('shows the server field errors as sent next to the matching fields on a 400', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(400, {
        errors: {
          email: ['Email is not valid.'],
          PASSWORD: [
            'Passwords must have a digit.',
            'Passwords must have a symbol.',
          ],
        },
      }),
    )
    renderRegister()

    await fillAndSubmit(user)

    expect(await screen.findByLabelText('Email')).toHaveAccessibleDescription(
      'Email is not valid.',
    )
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription(
      'Passwords must have a digit. Passwords must have a symbol.',
    )
  })

  it('shows the email taken message with a link to log in on a 409 and no technical text', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(409, { title: 'Conflict', status: 409 }))
    renderRegister()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(TAKEN)
    expect(alert).not.toHaveTextContent(/409|conflict/i)
    const link = within(alert.parentElement as HTMLElement).getByRole('link', {
      name: 'Log in',
    })
    expect(link).toHaveAttribute('href', '/login')
  })

  it('shows the generic friendly message when the network is down', async () => {
    const user = userEvent.setup()
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    renderRegister()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(GENERIC)
    expect(alert).not.toHaveTextContent(/failed to fetch|typeerror/i)
  })

  it('shows the generic friendly message on an unexpected status', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(500, { title: 'Server exploded' }))
    renderRegister()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(GENERIC)
    expect(alert).not.toHaveTextContent(/500|exploded/i)
  })

  it('disables the submit button and shows the loading text while the request runs', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderRegister()

    await fillAndSubmit(user)

    const button = screen.getByRole('button', { name: 'Please wait...' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByLabelText('Email')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Password')).toHaveAttribute('readonly')

    pending.resolve(okResult(testUser, 201))
    await screen.findByRole('heading', { level: 1, name: 'Log in' })
  })

  it('sends only one request when the button is pressed twice quickly', async () => {
    const user = userEvent.setup()
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderRegister()
    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!')
    await user.type(screen.getByLabelText('Public name'), 'Sara Test')

    await user.dblClick(screen.getByRole('button', { name: 'Create account' }))

    expect(post).toHaveBeenCalledTimes(1)
    pending.resolve(okResult(testUser, 201))
    await screen.findByRole('heading', { level: 1, name: 'Log in' })
  })

  it('keeps the email and clears the password after a failed register', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(409))
    renderRegister()

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
    renderRegister()

    await fillAndSubmit(user, 'sara@example.com', 'Sup3rSecret!')

    await screen.findByText('Password is not valid.')
    expect(screen.getByRole('main')).not.toHaveTextContent('Sup3rSecret!')
  })

  it('works with the keyboard only: tab to the fields and press Enter to submit', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser, 201))
    renderRegister()
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
    await user.keyboard('Secret1!')
    await user.tab()
    expect(screen.getByLabelText('Public name')).toHaveFocus()
    await user.keyboard('Sara Test{Enter}')

    expect(post).toHaveBeenCalledWith('/api/auth/register', {
      body: {
        email: 'sara@example.com',
        password: 'Secret1!',
        displayName: 'Sara Test',
      },
    })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Log in' }),
    ).toBeInTheDocument()
  })

  it('shows the page as normal without redirect for a logged-in user', async () => {
    get.mockResolvedValue(okResult(testUser))

    renderRegister()

    expect(
      await screen.findByRole('button', { name: 'Log out' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Create your account' }),
    ).toBeInTheDocument()
  })
})

describe('RegisterPage focus after a failed submit', () => {
  it('moves focus to the password field when only the password is empty', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(screen.getByLabelText('Password')).toHaveFocus()
  })

  it('moves focus to the email field when the server rejects the email', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(400, { errors: { Email: ['Email is not valid.'] } }),
    )
    renderRegister()

    await fillAndSubmit(user)

    await screen.findByText('Email is not valid.')
    expect(screen.getByLabelText('Email')).toHaveFocus()
  })

  it('moves focus to the error message when the email is taken', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(failResult(409))
    renderRegister()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(TAKEN)
    expect(document.activeElement).toContainElement(alert)
  })
})

describe('RegisterPage public name', () => {
  const INVALID =
    'Please use 3 to 30 characters: letters, digits, single spaces, hyphens or underscores.'
  const NAME_TAKEN = 'This name is already taken. Please choose another.'

  it('shows the Public name field last with the rules hint', async () => {
    renderRegister()

    const name = await screen.findByLabelText('Public name')
    expect(name).toHaveAccessibleDescription(
      '3 to 30 characters. Letters, digits, spaces, hyphens (-) and underscores (_). Other people will see this name.',
    )
    const form = name.closest('form') as HTMLFormElement
    const inputs = Array.from(form.querySelectorAll('input'))
    expect(inputs.map((input) => input.id)).toEqual([
      screen.getByLabelText('Email').id,
      screen.getByLabelText('Password').id,
      name.id,
    ])
  })

  it('sends the trimmed public name', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser, 201))
    renderRegister()

    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!')
    await user.type(screen.getByLabelText('Public name'), '  Sara Test  ')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(post).toHaveBeenCalledWith('/api/auth/register', {
      body: {
        email: 'sara@example.com',
        password: 'Secret1!',
        displayName: 'Sara Test',
      },
    })
  })

  it.each(['', '   '])(
    'does not send the form when the public name is "%s"',
    async (text) => {
      const user = userEvent.setup()
      renderRegister()

      await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
      await user.type(screen.getByLabelText('Password'), 'Secret1!')
      if (text) await user.type(screen.getByLabelText('Public name'), text)
      await user.click(screen.getByRole('button', { name: 'Create account' }))

      expect(post).not.toHaveBeenCalled()
      expect(screen.getByLabelText('Public name')).toHaveAccessibleDescription(
        'Please choose a public name.',
      )
      expect(screen.getByLabelText('Public name')).toHaveFocus()
    },
  )

  it('sends a short name to the server without a client length error', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(okResult(testUser, 201))
    renderRegister()

    await user.type(await screen.findByLabelText('Email'), 'sara@example.com')
    await user.type(screen.getByLabelText('Password'), 'Secret1!')
    await user.type(screen.getByLabelText('Public name'), 'Al')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(post).toHaveBeenCalledTimes(1)
    expect(screen.queryByText(INVALID)).not.toBeInTheDocument()
  })

  it.each(['DisplayName', 'displayName'])(
    'shows the fixed friendly message next to the name on a 400 with key %s',
    async (key) => {
      const user = userEvent.setup()
      post.mockResolvedValue(
        failResult(400, {
          errors: {
            [key]: ['The DisplayName field must be between 3 and 30.'],
          },
        }),
      )
      renderRegister()

      await fillAndSubmit(user)

      await waitFor(() =>
        expect(
          screen.getByLabelText('Public name'),
        ).toHaveAccessibleDescription(INVALID),
      )
      expect(screen.getByRole('main')).not.toHaveTextContent(
        /DisplayName field/,
      )
      expect(screen.getByLabelText('Public name')).toHaveFocus()
    },
  )

  it('shows the name taken message next to the name on a 409 and focuses the field', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(409, { title: 'Display name already taken', status: 409 }),
    )
    renderRegister()

    await fillAndSubmit(user)

    await waitFor(() =>
      expect(screen.getByLabelText('Public name')).toHaveAccessibleDescription(
        NAME_TAKEN,
      ),
    )
    expect(screen.getByLabelText('Public name')).toHaveFocus()
    expect(screen.queryByText(TAKEN)).not.toBeInTheDocument()
  })

  it('still shows the email taken message with the Log in link on a 409 for the email', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue(
      failResult(409, { title: 'Email already registered', status: 409 }),
    )
    renderRegister()

    await fillAndSubmit(user)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(TAKEN)
    expect(
      within(alert.parentElement as HTMLElement).getByRole('link', {
        name: 'Log in',
      }),
    ).toHaveAttribute('href', '/login')
    expect(screen.queryByText(NAME_TAKEN)).not.toBeInTheDocument()
  })
})
