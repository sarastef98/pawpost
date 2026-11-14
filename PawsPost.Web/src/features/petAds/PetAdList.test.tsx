import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, okResult } from '../../test/apiResponses'
import PetAdList from './PetAdList'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

const ERROR_TEXT = 'We could not load the ads. Please try again.'

type Item = Record<string, unknown>

function ad(overrides: Item = {}): Item {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Adoption',
    species: 'Dog',
    price: null,
    location: 'Skopje',
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

function listBody(items: Item[], totalCount = items.length, page = 1) {
  return { items, totalCount, page, pageSize: 20 }
}

function UrlDisplay() {
  const location = useLocation()
  const navigate = useNavigate()
  return (
    <>
      <p>URL: {location.pathname + location.search}</p>
      <button type="button" onClick={() => void navigate(-1)}>
        Browser back
      </button>
    </>
  )
}

function renderList(url = '/') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <PetAdList />
      <UrlDisplay />
    </MemoryRouter>,
  )
}

function lastQuery() {
  const call = get.mock.calls[get.mock.calls.length - 1]
  return call[1].params.query
}

describe('PetAdList', () => {
  beforeEach(() => {
    get.mockReset()
  })

  describe('list and cards', () => {
    it('shows one card for each ad in the order returned', async () => {
      get.mockResolvedValue(
        okResult(
          listBody([
            ad({ id: 'a', title: 'First' }),
            ad({ id: 'b', title: 'Second' }),
            ad({ id: 'c', title: 'Third' }),
          ]),
        ),
      )
      renderList()

      const titles = await screen.findAllByRole('heading', { level: 3 })

      expect(titles.map((t) => t.textContent)).toEqual([
        'First',
        'Second',
        'Third',
      ])
    })

    it('shows the Sale badge and the euro price on a Sale card', async () => {
      get.mockResolvedValue(
        okResult(listBody([ad({ type: 'Sale', price: 150.5 })])),
      )
      renderList()

      expect(await screen.findByText('€150.50')).toBeInTheDocument()
      expect(
        within(screen.getByRole('list')).getByText('Sale'),
      ).toBeInTheDocument()
    })

    it('shows a Sale price sent as a string with separators', async () => {
      get.mockResolvedValue(
        okResult(listBody([ad({ type: 'Sale', price: '1200' })])),
      )
      renderList()

      expect(await screen.findByText('€1,200.00')).toBeInTheDocument()
    })

    it('shows no price on an Adoption card even when the response has one', async () => {
      get.mockResolvedValue(okResult(listBody([ad({ price: 100 })])))
      renderList()

      await screen.findByText('Friendly Rex')

      expect(screen.queryByText(/€/)).not.toBeInTheDocument()
    })

    it('shows no price for a Sale ad whose price is null', async () => {
      get.mockResolvedValue(
        okResult(listBody([ad({ type: 'Sale', price: null })])),
      )
      renderList()

      await screen.findByText('Friendly Rex')

      expect(screen.queryByText(/€|null|NaN/)).not.toBeInTheDocument()
    })

    it('shows no price for a Sale ad whose price is not a valid number', async () => {
      get.mockResolvedValue(
        okResult(listBody([ad({ type: 'Sale', price: 'abc' })])),
      )
      renderList()

      await screen.findByText('Friendly Rex')

      expect(screen.queryByText(/€|NaN/)).not.toBeInTheDocument()
    })

    it('shows an ad with an unexpected type with the Adoption badge and no price', async () => {
      get.mockResolvedValue(
        okResult(listBody([ad({ type: 'Swap', price: 50 })])),
      )
      renderList()

      await screen.findByText('Friendly Rex')

      expect(
        within(screen.getByRole('list')).getByText('Adoption'),
      ).toBeInTheDocument()
      expect(screen.queryByText(/€/)).not.toBeInTheDocument()
    })

    it('shows a card without a location and without null text', async () => {
      get.mockResolvedValue(okResult(listBody([ad({ location: null })])))
      renderList()

      await screen.findByText('Friendly Rex')

      expect(screen.queryByText(/null/)).not.toBeInTheDocument()
    })

    it('links each card to its details page', async () => {
      get.mockResolvedValue(okResult(listBody([ad({ id: 'ad-42' })])))
      renderList()

      expect(
        await screen.findByRole('link', { name: /Friendly Rex/ }),
      ).toHaveAttribute('href', '/pet-ads/ad-42')
    })

    it('never shows an owner email or phone', async () => {
      get.mockResolvedValue(
        okResult(
          listBody([
            ad({ ownerEmail: 'owner@example.com', phone: '070 123 456' }),
          ]),
        ),
      )
      renderList()

      await screen.findByText('Friendly Rex')

      expect(screen.queryByText(/owner@example\.com|070 123 456/)).toBeNull()
    })
  })

  describe('requests and filters', () => {
    it('asks for page 1 with page size 20 and no filters on first load', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()

      await screen.findByText('Friendly Rex')

      expect(get).toHaveBeenCalledTimes(1)
      expect(lastQuery()).toEqual({ page: 1, pageSize: 20 })
    })

    it('sends the type when the visitor picks Sale and applies', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.selectOptions(screen.getByLabelText('Type'), 'Sale')
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )

      await waitFor(() => expect(lastQuery().type).toBe('Sale'))
    })

    it('sends the trimmed species when the visitor types Dog and applies', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.type(screen.getByLabelText('Species'), '  Dog ')
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )

      await waitFor(() => expect(lastQuery().species).toBe('Dog'))
    })

    it('sends type and species together', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.selectOptions(screen.getByLabelText('Type'), 'Adoption')
      await userEvent.type(screen.getByLabelText('Species'), 'Cat')
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )

      await waitFor(() =>
        expect(lastQuery()).toEqual({
          page: 1,
          pageSize: 20,
          type: 'Adoption',
          species: 'Cat',
        }),
      )
    })

    it('leaves out an empty species and All types from the request', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList('/?type=Sale&species=Dog')
      await screen.findByText('Friendly Rex')

      await userEvent.selectOptions(screen.getByLabelText('Type'), '')
      await userEvent.clear(screen.getByLabelText('Species'))
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )

      await waitFor(() =>
        expect(lastQuery()).toEqual({ page: 1, pageSize: 20 }),
      )
    })

    it('goes back to page 1 when filters are applied on page 3', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 100, 3)))
      renderList('/?page=3')
      await screen.findByText('Friendly Rex')

      await userEvent.type(screen.getByLabelText('Species'), 'Dog')
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )

      await waitFor(() => expect(lastQuery().page).toBe(1))
      expect(screen.getByText('URL: /?species=Dog')).toBeInTheDocument()
    })

    it('keeps focus in the Species field after applying with Enter', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()
      await screen.findByText('Friendly Rex')
      const speciesField = screen.getByLabelText('Species')

      await userEvent.type(speciesField, 'Dog{Enter}')

      await waitFor(() => expect(lastQuery().species).toBe('Dog'))
      await screen.findByText('Friendly Rex')
      expect(document.activeElement).not.toBe(document.body)
      expect(document.activeElement).toBe(speciesField)
      expect(speciesField).toHaveValue('Dog')
    })

    it('keeps focus on the Apply filters button after it is pressed', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList()
      await screen.findByText('Friendly Rex')
      await userEvent.type(screen.getByLabelText('Species'), 'Dog')
      const applyButton = screen.getByRole('button', { name: 'Apply filters' })

      await userEvent.click(applyButton)

      await waitFor(() => expect(lastQuery().species).toBe('Dog'))
      await screen.findByText('Friendly Rex')
      expect(document.activeElement).toBe(applyButton)
    })

    it('fills the filter fields from the URL again after going back', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList('/?type=Sale')
      await screen.findByText('Friendly Rex')
      await userEvent.selectOptions(screen.getByLabelText('Type'), 'Adoption')
      await userEvent.type(screen.getByLabelText('Species'), 'Dog{Enter}')
      await screen.findByText('URL: /?type=Adoption&species=Dog')

      await userEvent.click(
        screen.getByRole('button', { name: 'Browser back' }),
      )

      expect(await screen.findByText('URL: /?type=Sale')).toBeInTheDocument()
      expect(screen.getByLabelText('Type')).toHaveValue('Sale')
      expect(screen.getByLabelText('Species')).toHaveValue('')
    })
  })

  describe('paging', () => {
    it('disables Previous and enables Next on the first page when there are more ads', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 45)))
      renderList()
      await screen.findByText('Friendly Rex')

      expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
      expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
    })

    it('disables Next on the last page and enables Previous', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 45, 3)))
      renderList('/?page=3')
      await screen.findByText('Friendly Rex')

      expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
      expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
    })

    it('asks for the next page when Next is pressed and updates the URL', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 45)))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.click(screen.getByRole('button', { name: 'Next' }))

      await waitFor(() => expect(lastQuery().page).toBe(2))
      expect(screen.getByText('URL: /?page=2')).toBeInTheDocument()
    })

    it('asks for the previous page when Previous is pressed', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 45, 3)))
      renderList('/?page=3')
      await screen.findByText('Friendly Rex')

      await userEvent.click(screen.getByRole('button', { name: 'Previous' }))

      await waitFor(() => expect(lastQuery().page).toBe(2))
    })

    it('shows Page 2 of 3 computed from totalCount sent as a string', async () => {
      get.mockResolvedValue(
        okResult({
          items: [ad()],
          totalCount: '41',
          page: '2',
          pageSize: '20',
        }),
      )
      renderList('/?page=2')

      expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    })

    it('moves focus to the Latest ads heading after the new page has loaded', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 45)))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.click(screen.getByRole('button', { name: 'Next' }))

      await waitFor(() =>
        expect(
          screen.getByRole('heading', { level: 2, name: 'Latest ads' }),
        ).toHaveFocus(),
      )
    })

    it('replaces the URL with the last page when the page is beyond the last', async () => {
      get.mockImplementation(
        (_path: string, options: { params: { query: { page: number } } }) =>
          Promise.resolve(
            okResult(
              options.params.query.page === 9
                ? listBody([], 45, 9)
                : listBody([ad()], 45, options.params.query.page),
            ),
          ),
      )
      renderList('/?page=9')

      expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
      expect(screen.getByText('URL: /?page=3')).toBeInTheDocument()
    })

    it('shows the empty state on page 1 when totalCount is 0 and the page is above 1', async () => {
      get.mockResolvedValue(okResult(listBody([], 0, 5)))
      renderList('/?page=5')

      expect(
        await screen.findByText(
          'There are no ads yet. Please check back soon.',
        ),
      ).toBeInTheDocument()
      expect(screen.getByText('URL: /')).toBeInTheDocument()
    })
  })

  describe('states', () => {
    it('shows a spinner and no cards while loading', () => {
      get.mockReturnValue(new Promise(() => {}))
      renderList()

      expect(screen.getByRole('status')).toHaveTextContent('Loading')
      expect(screen.queryByRole('heading', { level: 3 })).toBeNull()
    })

    it('does not show the old cards while the next page is loading', async () => {
      get.mockResolvedValueOnce(okResult(listBody([ad()], 45)))
      get.mockReturnValueOnce(new Promise(() => {}))
      renderList()
      await screen.findByText('Friendly Rex')

      await userEvent.click(screen.getByRole('button', { name: 'Next' }))

      expect(screen.getByRole('status')).toBeInTheDocument()
      expect(screen.queryByText('Friendly Rex')).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Next' })).toBeNull()
    })

    it('shows the empty state without Clear filters when there are no ads and no filters', async () => {
      get.mockResolvedValue(okResult(listBody([])))
      renderList()

      expect(
        await screen.findByText(
          'There are no ads yet. Please check back soon.',
        ),
      ).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Clear filters' })).toBeNull()
    })

    it('shows the empty state with Clear filters when filters match nothing', async () => {
      get.mockResolvedValue(okResult(listBody([])))
      renderList('/?species=Dragon')

      expect(
        await screen.findByText('No ads match your filters.'),
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Clear filters' }),
      ).toBeInTheDocument()
    })

    it('clears the filters, shows the list again and moves focus to the Type select', async () => {
      get.mockImplementation(
        (_path: string, options: { params: { query: { species?: string } } }) =>
          Promise.resolve(
            okResult(
              options.params.query.species
                ? listBody([])
                : listBody([ad({ title: 'Everything' })]),
            ),
          ),
      )
      renderList('/?species=Dragon')
      await userEvent.click(
        await screen.findByRole('button', { name: 'Clear filters' }),
      )

      expect(await screen.findByText('Everything')).toBeInTheDocument()
      expect(screen.getByText('URL: /')).toBeInTheDocument()
      expect(screen.getByLabelText('Type')).toHaveFocus()
    })

    it('shows a friendly error without technical details when the API fails', async () => {
      get.mockResolvedValue(
        failResult(500, { title: 'Boom', detail: 'stack trace' }),
      )
      renderList()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
      expect(screen.queryByText(/Boom|stack trace|500/)).toBeNull()
    })

    it('shows the same friendly error when the network is down', async () => {
      get.mockRejectedValue(new TypeError('Failed to fetch'))
      renderList()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
    })

    it('asks the API again and shows the ads when Try again is pressed', async () => {
      get.mockResolvedValueOnce(failResult(500))
      get.mockResolvedValueOnce(okResult(listBody([ad()])))
      renderList()

      await userEvent.click(
        await screen.findByRole('button', { name: 'Try again' }),
      )

      expect(await screen.findByText('Friendly Rex')).toBeInTheDocument()
      expect(get).toHaveBeenCalledTimes(2)
    })

    it('ignores a slow response of an older request that arrives after a newer one', async () => {
      const slow = deferred<ReturnType<typeof okResult>>()
      get.mockReturnValueOnce(slow.promise)
      get.mockResolvedValueOnce(okResult(listBody([ad({ title: 'Newer' })])))
      renderList()

      await userEvent.type(screen.getByLabelText('Species'), 'Dog')
      await userEvent.click(
        screen.getByRole('button', { name: 'Apply filters' }),
      )
      expect(await screen.findByText('Newer')).toBeInTheDocument()
      slow.resolve(okResult(listBody([ad({ title: 'Older' })])))

      await waitFor(() => expect(get).toHaveBeenCalledTimes(2))
      expect(screen.queryByText('Older')).not.toBeInTheDocument()
      expect(screen.getByText('Newer')).toBeInTheDocument()
    })

    it('ignores a response that arrives after the page is left', async () => {
      const pending = deferred<ReturnType<typeof okResult>>()
      get.mockReturnValueOnce(pending.promise)
      const { unmount } = render(
        <MemoryRouter>
          <PetAdList />
        </MemoryRouter>,
      )
      unmount()
      pending.resolve(okResult(listBody([ad()])))

      await new Promise((resolve) => setTimeout(resolve, 0))

      expect(screen.queryByText('Friendly Rex')).not.toBeInTheDocument()
    })

    it('shows the error state when the response has an invalid totalCount', async () => {
      get.mockResolvedValue(
        okResult({ items: [ad()], totalCount: 'many', page: 1, pageSize: 20 }),
      )
      renderList()

      expect(await screen.findByRole('alert')).toHaveTextContent(ERROR_TEXT)
      expect(screen.queryByText('Friendly Rex')).not.toBeInTheDocument()
    })

    it('announces the number of ads in the polite live region', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 57)))
      renderList()

      const summary = await screen.findByText('57 ads found')

      expect(summary).toHaveAttribute('aria-live', 'polite')
    })
  })

  describe('URL state', () => {
    it('restores type, species and page from the URL and sends them to the API', async () => {
      get.mockResolvedValue(okResult(listBody([ad()], 100, 2)))
      renderList('/?type=Sale&species=Dog&page=2')
      await screen.findByText('Friendly Rex')

      expect(lastQuery()).toEqual({
        page: 2,
        pageSize: 20,
        type: 'Sale',
        species: 'Dog',
      })
    })

    it('fills the filter fields from the URL', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList('/?type=Adoption&species=Cat')
      await screen.findByText('Friendly Rex')

      expect(screen.getByLabelText('Type')).toHaveValue('Adoption')
      expect(screen.getByLabelText('Species')).toHaveValue('Cat')
    })

    it.each(['5', 'Cat', 'sale'])(
      'ignores the invalid type %s in the URL and loads all types',
      async (value) => {
        get.mockResolvedValue(okResult(listBody([ad()])))
        renderList(`/?type=${value}`)
        await screen.findByText('Friendly Rex')

        expect(lastQuery()).toEqual({ page: 1, pageSize: 20 })
      },
    )

    it.each(['0', '-2', 'abc', '1.5'])(
      'ignores the invalid page %s in the URL and loads page 1',
      async (value) => {
        get.mockResolvedValue(okResult(listBody([ad()])))
        renderList(`/?page=${value}`)
        await screen.findByText('Friendly Rex')

        expect(lastQuery()).toEqual({ page: 1, pageSize: 20 })
      },
    )

    it('ignores a species in the URL longer than 50 characters', async () => {
      get.mockResolvedValue(okResult(listBody([ad()])))
      renderList(`/?species=${'a'.repeat(51)}`)
      await screen.findByText('Friendly Rex')

      expect(lastQuery()).toEqual({ page: 1, pageSize: 20 })
    })
  })
})
