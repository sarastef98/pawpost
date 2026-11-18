import { render, screen, waitFor, within } from '@testing-library/react'
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
  testUser,
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

type TestUser = { id: string; email: string } | null

function pageTree(url: string, status: AuthStatus, user: TestUser) {
  const value = {
    status,
    user,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }
  return (
    <AuthContext.Provider value={value}>
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
    </AuthContext.Provider>
  )
}

function renderPage(
  url = '/creators/creator-1',
  status: AuthStatus = 'loggedOut',
  user: TestUser = null,
) {
  const result = render(pageTree(url, status, user))
  return {
    // Same router (same position in the tree), new auth value.
    changeAuth(nextStatus: AuthStatus, nextUser: TestUser) {
      result.rerender(pageTree(url, nextStatus, nextUser))
    },
  }
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

  describe('own profile', () => {
    const owner = { id: 'creator-1', email: testUser.email }

    // Answers the public profile and /mine by URL.
    function answer(mine: unknown, profile?: unknown) {
      get.mockImplementation((url: string) =>
        Promise.resolve(
          url === '/api/pet-ads/mine'
            ? mine
            : (profile ?? okResult(creatorProfileBody('Anna', ads(2)))),
        ),
      )
    }

    function mineCalls() {
      return get.mock.calls.filter(([url]) => url === '/api/pet-ads/mine')
    }

    function myAds(count: number, from = 1) {
      return okResult({
        items: Array.from({ length: count }, (_, i) =>
          sampleListItem({ id: `mine-${from + i}`, title: `Mine ${from + i}` }),
        ),
        totalCount: count,
        page: 1,
        pageSize: 20,
      })
    }

    it('shows the My ads tab and the user ads when the owner opens their own profile', async () => {
      answer(myAds(2))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Anna' }),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('tablist', { name: 'My profile' }),
      ).toBeInTheDocument()
      expect(screen.getAllByRole('tab')).toHaveLength(2)
      expect(screen.getByRole('tab', { name: 'My ads' })).toHaveAttribute(
        'aria-selected',
        'true',
      )
      expect(await screen.findByText('Mine 1')).toBeInTheDocument()
      expect(screen.getByText('Mine 2')).toBeInTheDocument()
    })

    it('requests /api/pet-ads/mine on the own profile', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      await screen.findByText('Mine 1')

      expect(mineCalls()).toHaveLength(1)
      expect(mineCalls()[0][1].params.query).toEqual({ page: 1, pageSize: 20 })
    })

    it('recognises the own profile when the URL id differs only in letter case', async () => {
      answer(myAds(1))
      renderPage('/creators/CREATOR-1', 'loggedIn', owner)

      expect(await screen.findByText('Mine 1')).toBeInTheDocument()
      expect(screen.getByRole('tablist')).toBeInTheDocument()
    })

    it('does not show the public Ads heading or the public ads on the own profile', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      await screen.findByText('Mine 1')

      expect(
        screen.queryByRole('heading', { level: 2, name: 'Ads' }),
      ).toBeNull()
      expect(screen.queryByText('Ad 1')).toBeNull()
    })

    it('shows no tabs and makes no /mine request for a visitor', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1', 'loggedOut')

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()
      expect(screen.queryByRole('tablist')).toBeNull()
      expect(mineCalls()).toHaveLength(0)
    })

    it('shows no tabs and makes no /mine request for a logged-in user on another profile', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1', 'loggedIn', {
        id: 'someone-else',
        email: testUser.email,
      })

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()
      expect(
        screen.getByRole('heading', { level: 2, name: 'Ads' }),
      ).toBeInTheDocument()
      expect(screen.queryByRole('tablist')).toBeNull()
      expect(mineCalls()).toHaveLength(0)
    })

    it('shows the public view and makes no /mine request while the login state is loading', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1', 'loading')

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()
      expect(screen.queryByRole('tablist')).toBeNull()
      expect(mineCalls()).toHaveLength(0)
    })

    it('shows the tabs once the login state settles as the owner', async () => {
      answer(myAds(1))
      const page = renderPage('/creators/creator-1', 'loading')
      await screen.findByText('Ad 1')

      page.changeAuth('loggedIn', owner)

      expect(await screen.findByText('Mine 1')).toBeInTheDocument()
      expect(screen.getByRole('tablist')).toBeInTheDocument()
      expect(screen.queryByText('Ad 1')).toBeNull()
    })

    it('shows no tabs in the not found state, even for the owner', async () => {
      answer(myAds(1), failResult(404))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      expect(await screen.findByText(NOT_FOUND_TEXT)).toBeInTheDocument()
      expect(screen.queryByRole('tablist')).toBeNull()
      expect(mineCalls()).toHaveLength(0)
    })

    it('shows no tabs in the error state, even for the owner', async () => {
      answer(myAds(1), failResult(500))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
      expect(screen.queryByRole('tablist')).toBeNull()
      expect(mineCalls()).toHaveLength(0)
    })

    it('sends the owner to the login page when /mine answers 401', async () => {
      answer(failResult(401))
      renderPage('/creators/creator-1', 'loggedIn', owner)

      expect(
        await screen.findByRole('heading', { name: 'Login probe' }),
      ).toBeInTheDocument()
    })

    it('does not send a visitor to the login page when the public profile loads', async () => {
      answer(failResult(401))
      renderPage('/creators/creator-1', 'loggedOut')

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: 'Login probe' })).toBeNull()
    })

    it('ignores myAdsPage in a visitor URL', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1?myAdsPage=3', 'loggedOut')

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()
      expect(lastCall()[1].params.query).toEqual({ page: 1, pageSize: 20 })
      expect(mineCalls()).toHaveLength(0)
    })

    it('keeps the public page key when the owner pages through My ads', async () => {
      const user = userEvent.setup()
      get.mockImplementation(
        (url: string, options: { params: { query: { page: number } } }) => {
          if (url !== '/api/pet-ads/mine') {
            return Promise.resolve(
              okResult(creatorProfileBody('Anna', ads(20), 45, 2)),
            )
          }
          const page = options.params.query.page
          return Promise.resolve(
            okResult({
              items: [
                sampleListItem({
                  id: `mine-${page}`,
                  title: `Mine page ${page}`,
                }),
              ],
              totalCount: 45,
              page,
              pageSize: 20,
            }),
          )
        },
      )
      renderPage('/creators/creator-1?page=2', 'loggedIn', owner)

      await user.click(await screen.findByRole('button', { name: 'Next' }))

      expect(await screen.findByText('Mine page 2')).toBeInTheDocument()
      expect(
        screen.getByText('URL: /creators/creator-1?page=2&myAdsPage=2'),
      ).toBeInTheDocument()
    })

    it('uses myAdsPage for My ads and page for the public profile request', async () => {
      answer(myAds(1))
      renderPage('/creators/creator-1?page=2&myAdsPage=3', 'loggedIn', owner)

      await screen.findByText('Mine 1')

      expect(mineCalls()[0][1].params.query).toEqual({ page: 3, pageSize: 20 })
      const publicCall = get.mock.calls.find(
        ([url]) => url !== '/api/pet-ads/mine',
      )
      expect(publicCall?.[1].params.query).toEqual({ page: 2, pageSize: 20 })
    })
  })

  describe('closed ads tab', () => {
    const owner = { id: 'creator-1', email: testUser.email }
    const CLOSED_URL = '/api/pet-ads/mine/closed'

    function listResult(titles: string[]) {
      return okResult({
        items: titles.map((title, i) =>
          sampleListItem({ id: `${title}-${i}`, title }),
        ),
        totalCount: titles.length,
        page: 1,
        pageSize: 20,
      })
    }

    // Answers the public profile, /mine and /mine/closed by URL. `closed` is
    // a list of results, one per request; the last one repeats.
    function answer(closed: unknown[] = [listResult([])]) {
      let closedCalls = 0
      get.mockImplementation((url: string) => {
        if (url === '/api/pet-ads/mine') {
          return Promise.resolve(listResult(['Mine 1']))
        }
        if (url === CLOSED_URL) {
          const result = closed[Math.min(closedCalls, closed.length - 1)]
          closedCalls += 1
          return Promise.resolve(result)
        }
        return Promise.resolve(okResult(creatorProfileBody('Anna', ads(2))))
      })
    }

    function closedCalls() {
      return get.mock.calls.filter(([url]) => url === CLOSED_URL)
    }

    it('shows the tabs My ads and Closed ads on the own profile, with My ads selected', async () => {
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)

      await screen.findByText('Mine 1')

      const tabs = screen.getAllByRole('tab')
      expect(tabs.map((tab) => tab.textContent)).toEqual([
        'My ads',
        'Closed ads',
      ])
      expect(screen.getByRole('tab', { name: 'My ads' })).toHaveAttribute(
        'aria-selected',
        'true',
      )
      expect(screen.getByRole('tab', { name: 'Closed ads' })).toHaveAttribute(
        'aria-selected',
        'false',
      )
    })

    it('recognises the own profile when the ids differ only in letter case', async () => {
      const user = userEvent.setup()
      answer([listResult(['Old Rex'])])
      renderPage('/creators/CREATOR-1', 'loggedIn', owner)

      await user.click(await screen.findByRole('tab', { name: 'Closed ads' }))

      expect(await screen.findByText('Old Rex')).toBeInTheDocument()
    })

    it('does not request the closed ads before the Closed ads tab is opened', async () => {
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)

      await screen.findByText('Mine 1')

      expect(closedCalls()).toHaveLength(0)
    })

    it('requests the closed ads and shows them in the tab panel when Closed ads is opened', async () => {
      const user = userEvent.setup()
      answer([listResult(['Old Rex', 'Old Mia'])])
      renderPage('/creators/creator-1', 'loggedIn', owner)

      await user.click(await screen.findByRole('tab', { name: 'Closed ads' }))

      expect(await screen.findByText('Old Rex')).toBeInTheDocument()
      expect(screen.getByText('Old Mia')).toBeInTheDocument()
      expect(closedCalls()).toHaveLength(1)
      expect(closedCalls()[0][1].params.query).toEqual({
        page: 1,
        pageSize: 20,
      })
      expect(screen.getByRole('tabpanel')).toHaveTextContent('Old Rex')
      expect(screen.queryByText('Mine 1')).toBeNull()
    })

    it('shows no tabs on another user profile and never requests the closed ads', async () => {
      answer()
      renderPage('/creators/creator-1', 'loggedIn', {
        id: 'someone-else',
        email: 'other@example.com',
      })

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()

      expect(screen.queryByRole('tab')).toBeNull()
      expect(closedCalls()).toHaveLength(0)
    })

    it('shows no tabs for a visitor, never requests the closed ads and does not redirect to login', async () => {
      answer()
      renderPage('/creators/creator-1', 'loggedOut')

      expect(await screen.findByText('Ad 1')).toBeInTheDocument()

      expect(screen.queryByRole('tab')).toBeNull()
      expect(closedCalls()).toHaveLength(0)
      expect(screen.queryByRole('heading', { name: 'Login probe' })).toBeNull()
    })

    it(
      'requests the closed list again and shows the ad after it was closed in My ads',
      { timeout: 15_000 },
      async () => {
        const user = userEvent.setup()
        const post = client.POST as unknown as Mock
        post.mockReset()
        post.mockResolvedValue({
          data: undefined,
          error: undefined,
          response: { status: 204, ok: true },
        })
        answer([listResult([]), listResult(['Mine 1'])])
        renderPage('/creators/creator-1', 'loggedIn', owner)
        await user.click(await screen.findByRole('tab', { name: 'Closed ads' }))
        expect(
          await screen.findByText('You have no closed ads.'),
        ).toBeInTheDocument()
        await user.click(screen.getByRole('tab', { name: 'My ads' }))
        await user.click(
          await screen.findByRole('button', { name: 'Close ad: Mine 1' }),
        )
        const dialog = await screen.findByRole('dialog')
        await user.click(
          within(dialog).getByRole('button', { name: 'Close ad' }),
        )
        await screen.findByText('Your ad was closed.')

        await user.click(screen.getByRole('tab', { name: 'Closed ads' }))

        await waitFor(() => expect(closedCalls()).toHaveLength(2))
        const panel = screen.getByRole('tabpanel')
        expect(await within(panel).findByText('Closed')).toBeInTheDocument()
        expect(within(panel).getByText('Mine 1')).toBeInTheDocument()
        expect(within(panel).queryByText('You have no closed ads.')).toBeNull()
      },
    )

    it('has two tabs in a tablist, with aria-selected following the selection', async () => {
      const user = userEvent.setup()
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)
      await screen.findByText('Mine 1')
      const list = screen.getByRole('tablist', { name: 'My profile' })
      expect(within(list).getAllByRole('tab')).toHaveLength(2)

      await user.click(screen.getByRole('tab', { name: 'Closed ads' }))

      expect(screen.getByRole('tab', { name: 'Closed ads' })).toHaveAttribute(
        'aria-selected',
        'true',
      )
      expect(screen.getByRole('tab', { name: 'My ads' })).toHaveAttribute(
        'aria-selected',
        'false',
      )
    })

    it('links each tab to its panel and labels the panel by its tab', async () => {
      const user = userEvent.setup()
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)
      await screen.findByText('Mine 1')
      const myAdsTab = screen.getByRole('tab', { name: 'My ads' })
      const closedTab = screen.getByRole('tab', { name: 'Closed ads' })
      const myAdsPanel = screen.getByRole('tabpanel')
      expect(myAdsTab.getAttribute('aria-controls')).toBe(myAdsPanel.id)
      expect(myAdsPanel.getAttribute('aria-labelledby')).toBe(myAdsTab.id)

      await user.click(closedTab)

      const closedPanel = screen.getByRole('tabpanel')
      expect(closedTab.getAttribute('aria-controls')).toBe(closedPanel.id)
      expect(closedPanel.getAttribute('aria-labelledby')).toBe(closedTab.id)
      expect(closedPanel.id).not.toBe(myAdsPanel.id)
    })

    it('moves between the two tabs with ArrowRight and ArrowLeft and wraps', async () => {
      const user = userEvent.setup()
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)
      await screen.findByText('Mine 1')
      const myAdsTab = screen.getByRole('tab', { name: 'My ads' })
      const closedTab = screen.getByRole('tab', { name: 'Closed ads' })
      myAdsTab.focus()

      await user.keyboard('{ArrowRight}')
      expect(closedTab).toHaveFocus()
      expect(closedTab).toHaveAttribute('aria-selected', 'true')
      await user.keyboard('{ArrowRight}')
      expect(myAdsTab).toHaveFocus()
      expect(myAdsTab).toHaveAttribute('aria-selected', 'true')
      await user.keyboard('{ArrowLeft}')
      expect(closedTab).toHaveFocus()
      expect(closedTab).toHaveAttribute('aria-selected', 'true')
    })

    it('selects My ads with Home and Closed ads with End and moves focus with the selection', async () => {
      const user = userEvent.setup()
      answer()
      renderPage('/creators/creator-1', 'loggedIn', owner)
      await screen.findByText('Mine 1')
      const myAdsTab = screen.getByRole('tab', { name: 'My ads' })
      const closedTab = screen.getByRole('tab', { name: 'Closed ads' })
      myAdsTab.focus()

      await user.keyboard('{End}')
      expect(closedTab).toHaveFocus()
      expect(closedTab).toHaveAttribute('aria-selected', 'true')
      await user.keyboard('{Home}')

      expect(myAdsTab).toHaveFocus()
      expect(myAdsTab).toHaveAttribute('aria-selected', 'true')
    })
  })
})
