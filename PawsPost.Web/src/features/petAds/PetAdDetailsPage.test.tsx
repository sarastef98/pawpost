import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, okResult } from '../../test/apiResponses'
import { AuthContext } from '../auth/useAuth'
import type { AuthStatus } from '../auth/useAuth'
import PetAdDetailsPage from './PetAdDetailsPage'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

const ERROR_TEXT = 'We could not load this ad. Please try again.'
const NOT_FOUND_TEXT = 'We could not find this ad. It may have been removed.'

function adBody(overrides: Record<string, unknown> = {}) {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Sale',
    species: 'Dog',
    breed: 'Labrador',
    ageInMonths: 27,
    description: 'A calm dog.',
    price: 150.5,
    location: 'Skopje',
    status: 'Active',
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

function GoToOther() {
  const navigate = useNavigate()
  return (
    <button type="button" onClick={() => void navigate('/pet-ads/ad-2')}>
      Open other ad
    </button>
  )
}

function renderPage(status: AuthStatus = 'loggedIn', url = '/pet-ads/ad-1') {
  const value = {
    status,
    user:
      status === 'loggedIn' ? { id: 'u1', email: 'sara@example.com' } : null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }
  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/login" element={<h1>Login probe</h1>} />
          <Route path="/" element={<h1>Home probe</h1>} />
          <Route path="/creators/:id" element={<h1>Creator probe</h1>} />
          <Route
            path="/pet-ads/:id"
            element={
              <>
                <PetAdDetailsPage />
                <GoToOther />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('PetAdDetailsPage', () => {
  beforeEach(() => {
    get.mockReset()
  })

  describe('content', () => {
    it('shows all fields of a Sale ad', async () => {
      get.mockResolvedValue(okResult(adBody()))
      renderPage()

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' }),
      ).toBeInTheDocument()
      expect(screen.getByText('Sale')).toBeInTheDocument()
      expect(screen.getByText('€150.50')).toBeInTheDocument()
      expect(screen.getByText('Dog')).toBeInTheDocument()
      expect(screen.getByText('Labrador')).toBeInTheDocument()
      expect(screen.getByText('2 years 3 months')).toBeInTheDocument()
      expect(screen.getByText('A calm dog.')).toBeInTheDocument()
      expect(screen.getByText('Skopje')).toBeInTheDocument()
    })

    it('shows an Adoption ad without a price and without any price text', async () => {
      get.mockResolvedValue(okResult(adBody({ type: 'Adoption', price: null })))
      renderPage()

      expect(await screen.findByText('Adoption')).toBeInTheDocument()
      expect(screen.queryByText(/€/)).not.toBeInTheDocument()
    })

    it('hides the breed and location rows when the API sends null', async () => {
      get.mockResolvedValue(okResult(adBody({ breed: null, location: null })))
      renderPage()

      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
      expect(screen.queryByText('Breed')).not.toBeInTheDocument()
      expect(screen.queryByText('Location')).not.toBeInTheDocument()
    })

    it('shows a description with HTML characters as text, not as markup', async () => {
      get.mockResolvedValue(
        okResult(adBody({ description: '<b>bold</b> <img src=x>' })),
      )
      renderPage()

      expect(
        await screen.findByText('<b>bold</b> <img src=x>'),
      ).toBeInTheDocument()
      expect(screen.queryByRole('img')).not.toBeInTheDocument()
    })
  })

  describe('request', () => {
    it('requests the ad with the id from the URL', async () => {
      get.mockResolvedValue(okResult(adBody({ id: 'abc-123' })))
      renderPage('loggedIn', '/pet-ads/abc-123')

      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
      expect(get).toHaveBeenCalledWith('/api/pet-ads/{id}', {
        params: { path: { id: 'abc-123' } },
      })
    })

    it('renders a new ad when the route id changes, with no stale ad', async () => {
      get.mockResolvedValueOnce(okResult(adBody()))
      const next = deferred<ReturnType<typeof okResult>>()
      get.mockReturnValueOnce(next.promise)
      const user = userEvent.setup()
      renderPage()
      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })

      await user.click(screen.getByRole('button', { name: 'Open other ad' }))

      expect(screen.queryByText('Friendly Rex')).not.toBeInTheDocument()
      expect(screen.getByRole('status')).toBeInTheDocument()
      next.resolve(okResult(adBody({ id: 'ad-2', title: 'Milo the cat' })))
      expect(
        await screen.findByRole('heading', { level: 1, name: 'Milo the cat' }),
      ).toBeInTheDocument()
      expect(get).toHaveBeenLastCalledWith('/api/pet-ads/{id}', {
        params: { path: { id: 'ad-2' } },
      })
    })
  })

  describe('login guard', () => {
    it('redirects a logged-out visitor to the login page without calling the ad endpoint', async () => {
      renderPage('loggedOut')

      expect(
        await screen.findByRole('heading', { name: 'Login probe' }),
      ).toBeInTheDocument()
      expect(get).not.toHaveBeenCalled()
    })

    it('shows a spinner while the login state is loading and makes no request', () => {
      renderPage('loading')

      expect(screen.getByRole('status')).toBeInTheDocument()
      expect(screen.queryByText('Login probe')).not.toBeInTheDocument()
      expect(get).not.toHaveBeenCalled()
    })

    it('shows the neutral heading while the login state is loading', () => {
      renderPage('loading')

      expect(
        screen.getByRole('heading', { level: 1, name: 'Ad details' }),
      ).toBeInTheDocument()
    })

    it('redirects to the login page when the API answers 401', async () => {
      get.mockResolvedValue(failResult(401))
      renderPage()

      expect(
        await screen.findByRole('heading', { name: 'Login probe' }),
      ).toBeInTheDocument()
    })
  })

  describe('states', () => {
    it('shows a spinner and the neutral heading while the ad is loading', () => {
      get.mockReturnValue(new Promise(() => {}))
      renderPage()

      expect(screen.getByRole('status')).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 1, name: 'Ad details' }),
      ).toBeInTheDocument()
    })

    it('shows the not found message with a link to the home page on 404', async () => {
      get.mockResolvedValue(failResult(404))
      renderPage()

      expect(await screen.findByText(NOT_FOUND_TEXT)).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'Go to the home page' }),
      ).toHaveAttribute('href', '/')
      expect(
        screen.getByRole('heading', { level: 1, name: 'Ad details' }),
      ).toBeInTheDocument()
    })

    it('shows the not found message when the id is not valid (400)', async () => {
      get.mockResolvedValue(failResult(400))
      renderPage('loggedIn', '/pet-ads/abc')

      expect(await screen.findByText(NOT_FOUND_TEXT)).toBeInTheDocument()
    })

    it('shows an error message with Try again and no technical text on a server error', async () => {
      get.mockResolvedValue(failResult(500))
      renderPage()

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent(ERROR_TEXT)
      expect(alert).not.toHaveTextContent(/500|status/i)
      expect(
        screen.getByRole('button', { name: 'Try again' }),
      ).toBeInTheDocument()
    })

    it('shows an error message when the network fails', async () => {
      get.mockRejectedValue(new Error('network down'))
      renderPage()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
      expect(screen.queryByText(/network down/)).not.toBeInTheDocument()
    })

    it('shows an error message when the body has the wrong shape', async () => {
      get.mockResolvedValue(okResult({ id: 'ad-1' }))
      renderPage()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
    })

    it('requests the ad again on Try again and shows it when it succeeds', async () => {
      get.mockResolvedValueOnce(failResult(500))
      get.mockResolvedValueOnce(okResult(adBody()))
      const user = userEvent.setup()
      renderPage()

      await user.click(await screen.findByRole('button', { name: 'Try again' }))

      const heading = await screen.findByRole('heading', {
        level: 1,
        name: 'Friendly Rex',
      })
      expect(get).toHaveBeenCalledTimes(2)
      await waitFor(() => expect(heading).toHaveFocus())
    })
  })

  describe('back link', () => {
    it('points to the home page', async () => {
      get.mockResolvedValue(okResult(adBody()))
      renderPage()

      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
      expect(
        screen.getByRole('link', { name: 'Back to all ads' }),
      ).toHaveAttribute('href', '/')
    })

    it('can be reached with the keyboard', async () => {
      get.mockResolvedValue(okResult(adBody()))
      const user = userEvent.setup()
      renderPage()
      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })

      await user.tab()

      expect(
        screen.getByRole('link', { name: 'Back to all ads' }),
      ).toHaveFocus()
    })

    it('stays visible in the error state', async () => {
      get.mockResolvedValue(failResult(500))
      renderPage()

      await screen.findByRole('alert')
      expect(
        screen.getByRole('link', { name: 'Back to all ads' }),
      ).toBeInTheDocument()
    })

    it('leads to the home page when clicked', async () => {
      get.mockResolvedValue(okResult(adBody()))
      const user = userEvent.setup()
      renderPage()

      await user.click(
        await screen.findByRole('link', { name: 'Back to all ads' }),
      )

      expect(
        await screen.findByRole('heading', { name: 'Home probe' }),
      ).toBeInTheDocument()
    })
  })
})

function StateProbe() {
  const state: unknown = useLocation().state
  return <p>Router state: {JSON.stringify(state ?? null)}</p>
}

function LeaveButton() {
  const navigate = useNavigate()
  return (
    <button type="button" onClick={() => void navigate('/elsewhere')}>
      Leave
    </button>
  )
}

function BackButton() {
  const navigate = useNavigate()
  return (
    <button type="button" onClick={() => void navigate(-1)}>
      Go back
    </button>
  )
}

describe('PetAdDetailsPage notice', () => {
  beforeEach(() => {
    get.mockReset()
  })

  function renderWithState(state?: unknown) {
    const value = {
      status: 'loggedIn' as const,
      user: { id: 'u1', email: 'sara@example.com' },
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
    }
    render(
      <AuthContext.Provider value={value}>
        <MemoryRouter initialEntries={[{ pathname: '/pet-ads/ad-1', state }]}>
          <Routes>
            <Route
              path="/pet-ads/:id"
              element={
                <>
                  <PetAdDetailsPage />
                  <StateProbe />
                  <LeaveButton />
                </>
              }
            />
            <Route path="/elsewhere" element={<BackButton />} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>,
    )
  }

  it('shows the notice from router state in a status region', async () => {
    get.mockResolvedValue(okResult(adBody()))
    renderWithState({ notice: 'Your ad is live.' })

    expect(await screen.findByText('Your ad is live.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Your ad is live.')
  })

  it('clears the router state after reading the notice', async () => {
    get.mockResolvedValue(okResult(adBody()))
    renderWithState({ notice: 'Your ad is live.' })

    expect(await screen.findByText('Router state: null')).toBeInTheDocument()
    expect(await screen.findByText('Your ad is live.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Your ad is live.')
  })

  it('shows no notice when the page is opened again after the state was cleared', async () => {
    get.mockResolvedValue(okResult(adBody()))
    const user = userEvent.setup()
    renderWithState({ notice: 'Your ad is live.' })
    await screen.findByText('Your ad is live.')
    await screen.findByText('Router state: null')

    await user.click(screen.getByRole('button', { name: 'Leave' }))
    await user.click(await screen.findByRole('button', { name: 'Go back' }))

    await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
    expect(screen.getByText('Router state: null')).toBeInTheDocument()
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
    expect(screen.queryByText('Your ad is live.')).not.toBeInTheDocument()
  })

  it('shows no notice text without router state', async () => {
    get.mockResolvedValue(okResult(adBody()))
    renderWithState()

    await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})

describe('PetAdDetailsPage creator', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('shows Posted by with a link that opens the creator profile', async () => {
    get.mockResolvedValue(
      okResult(adBody({ creator: { id: 'c1', displayName: 'Anna' } })),
    )
    renderPage()

    const link = await screen.findByRole('link', { name: 'Anna' })
    expect(screen.getByText('Posted by')).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/creators/c1')

    await userEvent.click(link)

    expect(
      await screen.findByRole('heading', { name: 'Creator probe' }),
    ).toBeInTheDocument()
  })

  it('encodes the creator id in the link', async () => {
    get.mockResolvedValue(
      okResult(adBody({ creator: { id: 'a/b', displayName: 'Anna' } })),
    )
    renderPage()

    expect(await screen.findByRole('link', { name: 'Anna' })).toHaveAttribute(
      'href',
      '/creators/a%2Fb',
    )
  })

  it.each([
    ['missing', undefined],
    ['unreadable', { id: '', displayName: 5 }],
  ])(
    'hides the Posted by row and still shows the ad when the creator is %s',
    async (_name, creator) => {
      get.mockResolvedValue(okResult(adBody({ creator })))
      renderPage()

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' }),
      ).toBeInTheDocument()
      expect(screen.queryByText('Posted by')).not.toBeInTheDocument()
    },
  )
})
