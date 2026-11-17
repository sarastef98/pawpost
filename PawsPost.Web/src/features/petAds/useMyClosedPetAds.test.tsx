import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import {
  deferred,
  failResult,
  okResult,
  sampleListItem,
} from '../../test/apiResponses'
import { useMyClosedPetAds } from './useMyClosedPetAds'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

function listBody(titles: string[], totalCount = titles.length, page = 1) {
  return {
    items: titles.map((title, i) => sampleListItem({ id: `ad-${i}`, title })),
    totalCount,
    page,
    pageSize: 20,
  }
}

describe('useMyClosedPetAds', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('asks for the own closed ads with the page and page size 20 and returns the list on 200', async () => {
    get.mockResolvedValue(okResult(listBody(['Rex', 'Mia'])))

    const { result } = renderHook(() => useMyClosedPetAds(2))

    expect(result.current.state.status).toBe('loading')
    await waitFor(() => expect(result.current.state.status).toBe('success'))
    expect(get).toHaveBeenCalledWith('/api/pet-ads/mine/closed', {
      params: { query: { page: 2, pageSize: 20 } },
    })
    const state = result.current.state
    if (state.status === 'success') {
      expect(state.data.items.map((ad) => ad.title)).toEqual(['Rex', 'Mia'])
    }
  })

  it('returns unauthorized on 401', async () => {
    get.mockResolvedValue(failResult(401))

    const { result } = renderHook(() => useMyClosedPetAds(1))

    await waitFor(() =>
      expect(result.current.state.status).toBe('unauthorized'),
    )
  })

  it.each([500, 400])('returns error on status %i', async (status) => {
    get.mockResolvedValue(failResult(status))

    const { result } = renderHook(() => useMyClosedPetAds(1))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('returns error when the request is rejected', async () => {
    get.mockRejectedValue(new TypeError('Failed to fetch'))

    const { result } = renderHook(() => useMyClosedPetAds(1))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('returns error when the body has the wrong shape', async () => {
    get.mockResolvedValue(okResult({ items: 'nope' }))

    const { result } = renderHook(() => useMyClosedPetAds(1))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('shows loading again for a new page instead of the old page', async () => {
    const second = deferred<ReturnType<typeof okResult>>()
    get.mockResolvedValueOnce(okResult(listBody(['Page one'], 25, 1)))
    get.mockReturnValueOnce(second.promise)

    const { result, rerender } = renderHook(
      ({ page }) => useMyClosedPetAds(page),
      { initialProps: { page: 1 } },
    )
    await waitFor(() => expect(result.current.state.status).toBe('success'))

    rerender({ page: 2 })

    expect(result.current.state.status).toBe('loading')
    await act(async () => {
      second.resolve(okResult(listBody(['Page two'], 25, 2)))
    })
    await waitFor(() => expect(result.current.state.status).toBe('success'))
  })

  it('ignores a late response for an old page', async () => {
    const first = deferred<ReturnType<typeof okResult>>()
    const second = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise)

    const { result, rerender } = renderHook(
      ({ page }) => useMyClosedPetAds(page),
      { initialProps: { page: 1 } },
    )
    rerender({ page: 2 })
    await act(async () => {
      second.resolve(okResult(listBody(['Page two'], 25, 2)))
    })
    await waitFor(() => expect(result.current.state.status).toBe('success'))
    await act(async () => {
      first.resolve(okResult(listBody(['Page one'], 25, 1)))
    })

    const state = result.current.state
    expect(state.status).toBe('success')
    if (state.status === 'success') {
      expect(state.data.items[0].title).toBe('Page two')
    }
  })

  it('sends a new request when retry is called', async () => {
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(listBody(['Rex'])))

    const { result } = renderHook(() => useMyClosedPetAds(1))
    await waitFor(() => expect(result.current.state.status).toBe('error'))

    act(() => result.current.retry())

    await waitFor(() => expect(result.current.state.status).toBe('success'))
    expect(get).toHaveBeenCalledTimes(2)
  })
})
