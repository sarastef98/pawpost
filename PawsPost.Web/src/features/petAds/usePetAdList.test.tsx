import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, okResult } from '../../test/apiResponses'
import type { ListQuery } from './petAdListQuery'
import { usePetAdList } from './usePetAdList'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

function listBody(title: string, totalCount = 1) {
  return {
    items: [
      { id: title, title, type: 'Adoption', price: null, location: null },
    ],
    totalCount,
    page: 1,
    pageSize: 20,
  }
}

const first: ListQuery = { type: '', species: '', page: 1 }

describe('usePetAdList', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('asks for page 1 with page size 20 and no filters', async () => {
    get.mockResolvedValue(okResult(listBody('Rex')))

    renderHook(() => usePetAdList(first))

    await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
    expect(get).toHaveBeenCalledWith('/api/pet-ads', {
      params: { query: { page: 1, pageSize: 20 } },
    })
  })

  it('sends type and species when they are set', async () => {
    get.mockResolvedValue(okResult(listBody('Rex')))

    renderHook(() => usePetAdList({ type: 'Sale', species: 'Dog', page: 3 }))

    await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
    expect(get).toHaveBeenCalledWith('/api/pet-ads', {
      params: {
        query: { page: 3, pageSize: 20, type: 'Sale', species: 'Dog' },
      },
    })
  })

  it('is loading first and then has the data', async () => {
    get.mockResolvedValue(okResult(listBody('Rex')))

    const { result } = renderHook(() => usePetAdList(first))

    expect(result.current.state.status).toBe('loading')
    await waitFor(() => expect(result.current.state.status).toBe('success'))
  })

  it('reports an error when the API answers with a failure', async () => {
    get.mockResolvedValue(failResult(500))

    const { result } = renderHook(() => usePetAdList(first))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('reports an error when the client throws', async () => {
    get.mockRejectedValue(new Error('network'))

    const { result } = renderHook(() => usePetAdList(first))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('reports an error when the response is not usable', async () => {
    get.mockResolvedValue(
      okResult({ items: [], totalCount: 'many', page: 1, pageSize: 20 }),
    )

    const { result } = renderHook(() => usePetAdList(first))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('is loading again for a new query and does not keep the old data', async () => {
    get.mockResolvedValueOnce(okResult(listBody('Rex')))
    const next = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(next.promise)

    const { result, rerender } = renderHook(
      ({ query }) => usePetAdList(query),
      { initialProps: { query: first } },
    )
    await waitFor(() => expect(result.current.state.status).toBe('success'))

    rerender({ query: { ...first, page: 2 } })

    expect(result.current.state.status).toBe('loading')
    await act(async () => next.resolve(okResult(listBody('Milo'))))
    await waitFor(() => expect(result.current.state.status).toBe('success'))
  })

  it('ignores a slow older response that arrives after a newer one', async () => {
    const slow = deferred<ReturnType<typeof okResult>>()
    const fast = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise)

    const { result, rerender } = renderHook(
      ({ query }) => usePetAdList(query),
      { initialProps: { query: first } },
    )
    rerender({ query: { ...first, page: 2 } })
    await act(async () => fast.resolve(okResult(listBody('New'))))
    await act(async () => slow.resolve(okResult(listBody('Old'))))

    const state = result.current.state
    expect(state.status).toBe('success')
    if (state.status === 'success') {
      expect(state.data.items[0].title).toBe('New')
    }
  })

  it('does not update after it is unmounted', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(pending.promise)

    const { unmount } = renderHook(() => usePetAdList(first))
    unmount()

    await act(async () => pending.resolve(okResult(listBody('Rex'))))

    expect(get).toHaveBeenCalledTimes(1)
  })

  it('asks again with the same query when retry is called', async () => {
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(listBody('Rex')))

    const { result } = renderHook(() => usePetAdList(first))
    await waitFor(() => expect(result.current.state.status).toBe('error'))

    act(() => result.current.retry())

    await waitFor(() => expect(result.current.state.status).toBe('success'))
    expect(get).toHaveBeenCalledTimes(2)
  })
})
