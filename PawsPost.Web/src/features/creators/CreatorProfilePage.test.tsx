import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import {
  creatorProfileBody,
  deferred,
  failResult,
  okResult,
  sampleListItem,
} from '../../test/apiResponses'
import { AuthContext } from '../auth/useAuth'
import type { AuthStatus } from '../auth/useAuth'
import CreatorProfilePage from './CreatorProfilePage'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

const ERROR_TEXT = 'We could not load this profile. Please try again.'
const NOT_FOUND_TEXT = 'We could not find this person.'

function UrlDisplay() {
  const location = useLocation()
  return <p>URL: {location.pathname + location.search}</p>
}

function renderPage(
  url = '/creators/creator-1',
  status: AuthStatus = 'loggedOut',
) {
  const routes = (
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/login" element={<h1>Login probe</h1>} />
        <Route path="/" element={<h1>Home probe</h1>} />
        <Route
          path="/creators/:id"
          element={
            <>
              <CreatorProfilePage />
              <UrlDisplay />
            </>
          }
        />
      </Routes>
    </MemoryRouter>
  )
  const value = {
    status,
    user: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }
  render(<AuthContext.Provider value={value}>{routes}</AuthContext.Provider>)
}

function ads(count: number, from = 1) {
  return Array.from({ length: count }, (_, i) =>
    sampleListItem({ id: `ad-${from + i}`, title: `Ad ${from + i}` }),
  )
}

function lastCall() {
  return get.mock.calls[get.mock.calls.length - 1]
}

describe('CreatorProfilePage', () => {
  beforeEach(() => {
    get.mockReset()
  })

  describe('content', () => {
    it("shows the creator's name as the heading and their ads as cards", async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', ads(2))))
      renderPage()

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Anna' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 2, name: 'Ads' }),
      ).toBeInTheDocument()
      expect(screen.getByText('Ad 1')).toBeInTheDocument()
      expect(screen.getByText('Ad 2')).toBeInTheDocument()
    })

    it('shows "By <name>" on each card', async () => {
      get.mockResolvedValue(
        okResult(
          creatorProfileBody('Anna', [
            sampleListItem({
              id: 'a',
              creator: { id: 'creator-1', displayName: 'Anna' },
            }),
            sampleListItem({
              id: 'b',
              creator: { id: 'creator-1', displayName: 'Anna' },
            }),
          ]),
        ),
      )
      renderPage()

      expect(await screen.findAllByText('By Anna')).toHaveLength(2)
    })

    it('shows the ads in the order the API returns them', async () => {
      get.mockResolvedValue(
        okResult(
          creatorProfileBody('Anna', [
            sampleListItem({ id: 'c', title: 'Third by id' }),
            sampleListItem({ id: 'a', title: 'First by id' }),
            sampleListItem({ id: 'b', title: 'Second by id' }),
          ]),
        ),
      )
      renderPage()

      const titles = await screen.findAllByRole('heading', { level: 3 })

      expect(titles.map((t) => t.textContent)).toEqual([
        'Third by id',
        'First by id',
        'Second by id',
      ])
    })

    it('links each card to its details page', async () => {
      get.mockResolvedValue(
        okResult(
          creatorProfileBody('Anna', [
            sampleListItem({ id: 'ad-42', title: 'Milo' }),
          ]),
        ),
      )
      renderPage()

      expect(await screen.findByRole('link', { name: /Milo/ })).toHaveAttribute(
        'href',
        '/pet-ads/ad-42',
      )
    })

    it('asks for the id from the URL, page 1 and page size 20', async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', ads(1))))
      renderPage('/creators/abc-123')

      await screen.findByRole('heading', { level: 1, name: 'Anna' })

      expect(lastCall()).toEqual([
        '/api/creators/{id}',
        {
          params: {
            path: { id: 'abc-123' },
            query: { page: 1, pageSize: 20 },
          },
        },
      ])
    })

    it('shows only the name and the ads, not extra fields from the response', async () => {
      get.mockResolvedValue(
        okResult({
          ...creatorProfileBody('Anna', ads(1)),
          email: 'secret.owner@example.com',
          phone: '+38970000000',
        }),
      )
      renderPage()

      await screen.findByRole('heading', { level: 1, name: 'Anna' })

      expect(document.body).not.toHaveTextContent('secret.owner@example.com')
      expect(document.body).not.toHaveTextContent('+38970000000')
    })

    it('shows a name with HTML characters as text and creates no element', async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('<b>Anna</b>', ads(1))))
      renderPage()

      expect(
        await screen.findByRole('heading', { level: 1 }),
      ).toHaveTextContent('<b>Anna</b>')
      expect(document.querySelector('b')).toBeNull()
    })

    it('leads back to the home page with the back link', async () => {
      const user = userEvent.setup()
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', ads(1))))
      renderPage()
      await screen.findByRole('heading', { level: 1, name: 'Anna' })

      await user.click(screen.getByRole('link', { name: 'Back to all ads' }))

      expect(
        await screen.findByRole('heading', { name: 'Home probe' }),
      ).toBeInTheDocument()
    })
  })

  describe('login', () => {
    it('loads the profile for a visitor who is not logged in without a redirect', async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', ads(1))))
      renderPage('/creators/creator-1', 'loggedOut')

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Anna' }),
      ).toBeInTheDocument()
      expect(screen.queryByText('Login probe')).not.toBeInTheDocument()
    })

    it('loads the profile while the login state is still loading', async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', ads(1))))
      renderPage('/creators/creator-1', 'loading')

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Anna' }),
      ).toBeInTheDocument()
    })
  })

  describe('states', () => {
    it('shows a spinner while the profile is loading', async () => {
      const pending = deferred<ReturnType<typeof okResult>>()
      get.mockReturnValue(pending.promise)
      renderPage()

      expect(screen.getByRole('status')).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 1, name: 'Creator profile' }),
      ).toBeInTheDocument()

      pending.resolve(okResult(creatorProfileBody('Anna', ads(1))))
      await screen.findByRole('heading', { level: 1, name: 'Anna' })
    })

    it('shows the empty state with a link to all ads and no pager when there are no ads', async () => {
      get.mockResolvedValue(okResult(creatorProfileBody('Anna', [])))
      renderPage()

      expect(
        await screen.findByText(
          'Anna has no ads right now. Please check back soon.',
        ),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'Browse all ads' }),
      ).toHaveAttribute('href', '/')
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
    })

    it('shows the not found state with a link to the home page on 404', async () => {
      get.mockResolvedValue(failResult(404))
      renderPage()

      expect(await screen.findByText(NOT_FOUND_TEXT)).toBeInTheDocument()
      expect(
        screen.getByRole('link', { name: 'Go to the home page' }),
      ).toHaveAttribute('href', '/')
    })

    it('shows the not found state on 400 for an invalid id', async () => {
      get.mockResolvedValue(failResult(400))
      renderPage('/creators/not-a-guid')

      expect(await screen.findByText(NOT_FOUND_TEXT)).toBeInTheDocument()
      expect(screen.queryByText(ERROR_TEXT)).not.toBeInTheDocument()
    })

    it('shows an error with Try again on a server error and no technical details', async () => {
      get.mockResolvedValue(
        failResult(500, { title: 'Server exploded', status: 500 }),
      )
      renderPage()

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent(ERROR_TEXT)
      expect(alert).not.toHaveTextContent(/500|exploded/i)
      expect(
        screen.getByRole('button', { name: 'Try again' }),
      ).toBeInTheDocument()
    })

    it('shows an error when the API is unreachable', async () => {
      get.mockRejectedValue(new TypeError('Failed to fetch'))
      renderPage()

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent(ERROR_TEXT)
      expect(alert).not.toHaveTextContent(/failed to fetch/i)
    })

    it('shows an error when the response has the wrong shape', async () => {
      get.mockResolvedValue(okResult({ id: 'creator-1' }))
      renderPage()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
    })

    it('loads the profile again when Try again is pressed', async () => {
      const user = userEvent.setup()
      get.mockResolvedValueOnce(failResult(500))
      get.mockResolvedValueOnce(okResult(creatorProfileBody('Anna', ads(1))))
      renderPage()

      await user.click(await screen.findByRole('button', { name: 'Try again' }))

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Anna' }),
      ).toBeInTheDocument()
      expect(get).toHaveBeenCalledTimes(2)
    })
  })

  describe('paging', () => {
    it('disables Previous on the first page', async () => {
      get.mockResolvedValue(
        okResult(creatorProfileBody('Anna', ads(20), 45, 1)),
      )
      renderPage()

      expect(
        await screen.findByRole('button', { name: 'Previous' }),
      ).toBeDisabled()
      expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
    })

    it('requests page 2 on Next, shows its ads and updates the URL', async () => {
      const user = userEvent.setup()
      get.mockResolvedValueOnce(
        okResult(creatorProfileBody('Anna', ads(20), 45, 1)),
      )
      get.mockResolvedValueOnce(
        okResult(creatorProfileBody('Anna', ads(20, 21), 45, 2)),
      )
      renderPage()

      await user.click(await screen.findByRole('button', { name: 'Next' }))

      expect(await screen.findByText('Ad 21')).toBeInTheDocument()
      expect(screen.queryByText('Ad 1')).not.toBeInTheDocument()
      expect(lastCall()[1].params.query).toEqual({ page: 2, pageSize: 20 })
      expect(
        screen.getByText('URL: /creators/creator-1?page=2'),
      ).toBeInTheDocument()
    })

    it('disables Next on the last page', async () => {
      get.mockResolvedValue(
        okResult(creatorProfileBody('Anna', ads(5, 41), 45, 3)),
      )
      renderPage('/creators/creator-1?page=3')

      expect(await screen.findByRole('button', { name: 'Next' })).toBeDisabled()
      expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
    })

    it('opens the page from the URL and requests it', async () => {
      get.mockResolvedValue(
        okResult(creatorProfileBody('Anna', ads(20, 21), 45, 2)),
      )
      renderPage('/creators/creator-1?page=2')

      expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
      expect(lastCall()[1].params.query).toEqual({ page: 2, pageSize: 20 })
    })

    it('replaces an out-of-range page with the last page', async () => {
      get.mockResolvedValueOnce(okResult(creatorProfileBody('Anna', [], 45, 9)))
      get.mockResolvedValueOnce(
        okResult(creatorProfileBody('Anna', ads(5, 41), 45, 3)),
      )
      renderPage('/creators/creator-1?page=9')

      expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
      expect(
        screen.getByText('URL: /creators/creator-1?page=3'),
      ).toBeInTheDocument()
      expect(lastCall()[1].params.query).toEqual({ page: 3, pageSize: 20 })
    })

    it('moves focus to the Ads heading after Next', async () => {
      const user = userEvent.setup()
      get.mockResolvedValueOnce(
        okResult(creatorProfileBody('Anna', ads(20), 45, 1)),
      )
      get.mockResolvedValueOnce(
        okResult(creatorProfileBody('Anna', ads(20, 21), 45, 2)),
      )
      renderPage()

      await user.click(await screen.findByRole('button', { name: 'Next' }))

      await screen.findByText('Ad 21')
      await waitFor(() =>
        expect(screen.getByRole('heading', { name: 'Ads' })).toHaveFocus(),
      )
    })
  })
})
