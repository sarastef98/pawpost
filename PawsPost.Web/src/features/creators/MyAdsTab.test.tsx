import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import Tabs from '../../components/Tabs'
import {
  deferred,
  failResult,
  okResult,
  sampleListItem,
} from '../../test/apiResponses'
import MyAdsTab from './MyAdsTab'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

const ERROR_TEXT = 'We could not load your ads. Please try again.'
const EMPTY_TEXT = 'You have no active ads right now.'

function UrlDisplay() {
  const location = useLocation()
  return <p>URL: {location.pathname + location.search}</p>
}

function renderTab(url = '/creators/user-1') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/login" element={<h1>Login probe</h1>} />
        <Route path="/pet-ads/new" element={<h1>New ad probe</h1>} />
        <Route
          path="/creators/:id"
          element={
            <>
              <Tabs
                label="My profile"
                items={[
                  {
                    id: 'my-ads',
                    label: 'My ads',
                    content: () => <MyAdsTab />,
                  },
                ]}
              />
              <UrlDisplay />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

function listBody(count: number, from = 1, totalCount = count, page = 1) {
  return {
    items: Array.from({ length: count }, (_, i) =>
      sampleListItem({ id: `ad-${from + i}`, title: `Ad ${from + i}` }),
    ),
    totalCount,
    page,
    pageSize: 20,
  }
}

function lastCall() {
  return get.mock.calls[get.mock.calls.length - 1]
}

describe('MyAdsTab', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('shows the ads as cards in the order the API returns them', async () => {
    get.mockResolvedValue(
      okResult({
        items: [
          sampleListItem({ id: 'c', title: 'Third by id' }),
          sampleListItem({ id: 'a', title: 'First by id' }),
          sampleListItem({ id: 'b', title: 'Second by id' }),
        ],
        totalCount: 3,
        page: 1,
        pageSize: 20,
      }),
    )
    renderTab()

    const titles = await screen.findAllByRole('heading', { level: 3 })

    expect(titles.map((t) => t.textContent)).toEqual([
      'Third by id',
      'First by id',
      'Second by id',
    ])
  })

  it('links each card to its details page', async () => {
    get.mockResolvedValue(
      okResult({
        items: [sampleListItem({ id: 'ad-42', title: 'Milo' })],
        totalCount: 1,
        page: 1,
        pageSize: 20,
      }),
    )
    renderTab()

    expect(await screen.findByRole('link', { name: /Milo/ })).toHaveAttribute(
      'href',
      '/pet-ads/ad-42',
    )
  })

  it('shows a Spinner while the ads load', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValue(pending.promise)
    renderTab()

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByText('Ad 1')).toBeNull()

    pending.resolve(okResult(listBody(1)))
    expect(await screen.findByText('Ad 1')).toBeInTheDocument()
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows the empty message with a Post an ad link and no pager when there are no ads', async () => {
    get.mockResolvedValue(okResult(listBody(0)))
    renderTab()

    expect(await screen.findByText(EMPTY_TEXT)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Post an ad' })).toHaveAttribute(
      'href',
      '/pet-ads/new',
    )
    expect(screen.queryByRole('navigation', { name: 'Pages' })).toBeNull()
  })

  it('shows an error with Try again and no technical details on a server error', async () => {
    get.mockResolvedValue(
      failResult(500, { title: 'Boom', detail: 'Stack trace here' }),
    )
    renderTab()

    expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeEnabled()
    expect(screen.queryByText(/Boom|Stack trace/)).toBeNull()
  })

  it('shows the error on a network failure', async () => {
    get.mockRejectedValue(new TypeError('Failed to fetch'))
    renderTab()

    expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
  })

  it('shows the error when the body has the wrong shape', async () => {
    get.mockResolvedValue(okResult({ items: 'nope' }))
    renderTab()

    expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
  })

  it('loads the ads again and shows them when Try again is pressed', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(listBody(2)))
    renderTab()

    await user.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Ad 1')).toBeInTheDocument()
    expect(screen.getByText('Ad 2')).toBeInTheDocument()
    expect(screen.queryByText(ERROR_TEXT)).toBeNull()
    expect(get).toHaveBeenCalledTimes(2)
  })

  it('moves focus to the tab panel after Try again', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(listBody(1)))
    renderTab()

    await user.click(await screen.findByRole('button', { name: 'Try again' }))

    await screen.findByText('Ad 1')
    await waitFor(() => expect(screen.getByRole('tabpanel')).toHaveFocus())
  })

  it('sends the user to the login page on 401', async () => {
    get.mockResolvedValue(failResult(401))
    renderTab()

    expect(
      await screen.findByRole('heading', { name: 'Login probe' }),
    ).toBeInTheDocument()
  })

  it('requests page 1 with page size 20 by default', async () => {
    get.mockResolvedValue(okResult(listBody(1)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(lastCall()[0]).toBe('/api/pet-ads/mine')
    expect(lastCall()[1].params.query).toEqual({ page: 1, pageSize: 20 })
  })

  it('disables Previous on the first page', async () => {
    get.mockResolvedValue(okResult(listBody(20, 1, 45, 1)))
    renderTab()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
  })

  it('disables Next on the last page', async () => {
    get.mockResolvedValue(okResult(listBody(5, 41, 45, 3)))
    renderTab('/creators/user-1?myAdsPage=3')

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('requests page 2, shows its ads and sets myAdsPage in the URL, keeping other keys, on Next', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    renderTab('/creators/user-1?page=4')

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Ad 21')).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 2, pageSize: 20 })
    expect(
      screen.getByText('URL: /creators/user-1?page=4&myAdsPage=2'),
    ).toBeInTheDocument()
  })

  it('goes back to page 1 and removes myAdsPage from the URL on Previous', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    renderTab('/creators/user-1?myAdsPage=2')

    await user.click(await screen.findByRole('button', { name: 'Previous' }))

    expect(await screen.findByText('Ad 1')).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 1, pageSize: 20 })
    expect(screen.getByText('URL: /creators/user-1')).toBeInTheDocument()
  })

  it('requests page 2 when the page is opened directly with myAdsPage=2', async () => {
    get.mockResolvedValue(okResult(listBody(20, 21, 45, 2)))
    renderTab('/creators/user-1?myAdsPage=2')

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 2, pageSize: 20 })
  })

  it('replaces an out-of-range myAdsPage with the last page', async () => {
    get.mockResolvedValueOnce(okResult(listBody(0, 1, 45, 9)))
    get.mockResolvedValueOnce(okResult(listBody(5, 41, 45, 3)))
    renderTab('/creators/user-1?myAdsPage=9')

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
    expect(
      screen.getByText('URL: /creators/user-1?myAdsPage=3'),
    ).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 3, pageSize: 20 })
  })

  it('removes an out-of-range myAdsPage when there are no ads at all', async () => {
    get.mockResolvedValueOnce(okResult(listBody(0, 1, 0, 9)))
    get.mockResolvedValueOnce(okResult(listBody(0)))
    renderTab('/creators/user-1?myAdsPage=9')

    expect(await screen.findByText(EMPTY_TEXT)).toBeInTheDocument()
    expect(screen.getByText('URL: /creators/user-1')).toBeInTheDocument()
  })

  it('moves focus to the tab panel after Next', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    renderTab()

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    await screen.findByText('Ad 21')
    await waitFor(() => expect(screen.getByRole('tabpanel')).toHaveFocus())
  })
})
