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
import ClosedAdsTab from './ClosedAdsTab'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

const ERROR_TEXT = 'We could not load your closed ads. Please try again.'
const EMPTY_TEXT = 'You have no closed ads.'

function UrlDisplay() {
  const location = useLocation()
  return <p>URL: {location.pathname + location.search}</p>
}

function renderTab(url = '/creators/user-1') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/login" element={<h1>Login probe</h1>} />
        <Route path="/pet-ads/:id" element={<h1>Details probe</h1>} />
        <Route
          path="/creators/:id"
          element={
            <>
              <Tabs
                label="My profile"
                items={[
                  {
                    id: 'closed-ads',
                    label: 'Closed ads',
                    content: () => <ClosedAdsTab />,
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

describe('ClosedAdsTab', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('shows a Spinner while the closed ads load', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValue(pending.promise)
    renderTab()

    expect(screen.getByText('Loading')).toBeInTheDocument()
    expect(screen.queryByText('Ad 1')).toBeNull()

    pending.resolve(okResult(listBody(1)))
    expect(await screen.findByText('Ad 1')).toBeInTheDocument()
    expect(screen.queryByText('Loading')).toBeNull()
  })

  it('requests the closed endpoint with page 1 and page size 20', async () => {
    get.mockResolvedValue(okResult(listBody(1)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(lastCall()[0]).toBe('/api/pet-ads/mine/closed')
    expect(lastCall()[1].params.query).toEqual({ page: 1, pageSize: 20 })
  })

  it('lists the closed ads in the order the API returns them', async () => {
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

  it('shows Closed on each card', async () => {
    get.mockResolvedValue(okResult(listBody(3)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(screen.getAllByText('Closed')).toHaveLength(3)
  })

  it('shows the closed cards without links', async () => {
    get.mockResolvedValue(okResult(listBody(2)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('does not navigate when a card is clicked', async () => {
    const user = userEvent.setup()
    get.mockResolvedValue(okResult(listBody(1)))
    renderTab()

    await user.click(await screen.findByText('Ad 1'))

    expect(screen.getByText('URL: /creators/user-1')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Details probe' })).toBeNull()
  })

  it('shows no Close ad, reopen, reserve or edit button', async () => {
    get.mockResolvedValue(okResult(listBody(2)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(
      screen.queryByRole('button', { name: /close|reopen|reserve|edit/i }),
    ).toBeNull()
  })

  it('shows no date on the cards', async () => {
    get.mockResolvedValue(okResult(listBody(1)))
    renderTab()

    await screen.findByText('Ad 1')

    expect(screen.queryByText(/2026|\d{1,2}[./]\d{1,2}[./]\d{2,4}/)).toBeNull()
  })

  it('shows the empty message and no pager when there are no closed ads', async () => {
    get.mockResolvedValue(okResult(listBody(0)))
    renderTab()

    expect(await screen.findByText(EMPTY_TEXT)).toBeInTheDocument()
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

  it('loads the list again and shows it when Try again is pressed', async () => {
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

  it('sends the user to the login page on 401', async () => {
    get.mockResolvedValue(failResult(401))
    renderTab()

    expect(
      await screen.findByRole('heading', { name: 'Login probe' }),
    ).toBeInTheDocument()
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
    renderTab('/creators/user-1?closedAdsPage=3')

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('requests page 2 and sets closedAdsPage in the URL on Next', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    renderTab()

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Ad 21')).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 2, pageSize: 20 })
    expect(
      screen.getByText('URL: /creators/user-1?closedAdsPage=2'),
    ).toBeInTheDocument()
  })

  it('goes back to page 1 and removes closedAdsPage from the URL on Previous', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    renderTab('/creators/user-1?closedAdsPage=2')

    await user.click(await screen.findByRole('button', { name: 'Previous' }))

    expect(await screen.findByText('Ad 1')).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 1, pageSize: 20 })
    expect(screen.getByText('URL: /creators/user-1')).toBeInTheDocument()
  })

  it('reads the start page from closedAdsPage and leaves page and myAdsPage untouched', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 65, 2)))
    get.mockResolvedValueOnce(okResult(listBody(20, 41, 65, 3)))
    renderTab('/creators/user-1?page=4&myAdsPage=5&closedAdsPage=2')

    expect(await screen.findByText('Page 2 of 4')).toBeInTheDocument()
    expect(get.mock.calls[0][1].params.query).toEqual({
      page: 2,
      pageSize: 20,
    })
    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Ad 41')).toBeInTheDocument()
    expect(
      screen.getByText(
        'URL: /creators/user-1?page=4&myAdsPage=5&closedAdsPage=3',
      ),
    ).toBeInTheDocument()
  })

  it('replaces an out-of-range closedAdsPage with the last page', async () => {
    get.mockResolvedValueOnce(okResult(listBody(0, 1, 45, 9)))
    get.mockResolvedValueOnce(okResult(listBody(5, 41, 45, 3)))
    renderTab('/creators/user-1?closedAdsPage=9')

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
    expect(
      screen.getByText('URL: /creators/user-1?closedAdsPage=3'),
    ).toBeInTheDocument()
    expect(lastCall()[1].params.query).toEqual({ page: 3, pageSize: 20 })
  })

  it('removes an out-of-range closedAdsPage when there are no closed ads at all', async () => {
    get.mockResolvedValueOnce(okResult(listBody(0, 1, 0, 9)))
    get.mockResolvedValueOnce(okResult(listBody(0)))
    renderTab('/creators/user-1?closedAdsPage=9')

    expect(await screen.findByText(EMPTY_TEXT)).toBeInTheDocument()
    expect(screen.getByText('URL: /creators/user-1')).toBeInTheDocument()
  })

  it('moves focus to the tab panel after changing page', async () => {
    const user = userEvent.setup()
    get.mockResolvedValueOnce(okResult(listBody(20, 1, 45, 1)))
    get.mockResolvedValueOnce(okResult(listBody(20, 21, 45, 2)))
    renderTab()

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    await screen.findByText('Ad 21')
    await waitFor(() => expect(screen.getByRole('tabpanel')).toHaveFocus())
  })
})
