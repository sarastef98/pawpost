import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, okResult } from '../../test/apiResponses'
import { usePetAd } from './usePetAd'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

function adBody(title = 'Rex') {
  return {
    id: 'ad-1',
    title,
    type: 'Adoption',
    species: 'Dog',
    breed: null,
    ageInMonths: 12,
    description: 'Nice',
    price: null,
    location: null,
    status: 'Active',
    createdAt: '2026-11-01T10:00:00Z',
  }
}

describe('usePetAd', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('does not call the API while enabled is false', async () => {
    const { result } = renderHook(() => usePetAd('ad-1', false))

    await act(async () => {})
    expect(get).not.toHaveBeenCalled()
    expect(result.current.state.status).toBe('loading')
  })

  it('asks for the ad with the id as a path parameter once enabled', async () => {
    get.mockResolvedValue(okResult(adBody()))

    const { rerender } = renderHook(({ on }) => usePetAd('ad-1', on), {
      initialProps: { on: false },
    })
    rerender({ on: true })

    await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
    expect(get).toHaveBeenCalledWith('/api/pet-ads/{id}', {
      params: { path: { id: 'ad-1' } },
    })
  })

  it('is loading first and then has the ad on 200', async () => {
    get.mockResolvedValue(okResult(adBody()))

    const { result } = renderHook(() => usePetAd('ad-1', true))

    expect(result.current.state.status).toBe('loading')
    await waitFor(() => expect(result.current.state.status).toBe('success'))
    const state = result.current.state
    if (state.status === 'success') expect(state.ad.title).toBe('Rex')
  })

  it.each([
    [401, 'unauthorized'],
    [404, 'notFound'],
    [400, 'notFound'],
    [500, 'error'],
  ])('maps a %i answer to %s', async (code, expected) => {
    get.mockResolvedValue(failResult(code))

    const { result } = renderHook(() => usePetAd('ad-1', true))

    await waitFor(() => expect(result.current.state.status).toBe(expected))
  })

  it('reports an error when the client throws', async () => {
    get.mockRejectedValue(new Error('network'))

    const { result } = renderHook(() => usePetAd('ad-1', true))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('reports an error when the body has the wrong shape', async () => {
    get.mockResolvedValue(okResult({ id: 'ad-1' }))

    const { result } = renderHook(() => usePetAd('ad-1', true))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('is loading again for a new id and does not keep the old ad', async () => {
    get.mockResolvedValueOnce(okResult(adBody('Rex')))
    const next = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(next.promise)

    const { result, rerender } = renderHook(({ id }) => usePetAd(id, true), {
      initialProps: { id: 'ad-1' },
    })
    await waitFor(() => expect(result.current.state.status).toBe('success'))

    rerender({ id: 'ad-2' })

    expect(result.current.state.status).toBe('loading')
    await act(async () => next.resolve(okResult(adBody('Milo'))))
    await waitFor(() => expect(result.current.state.status).toBe('success'))
  })

  it('ignores a slow older response after the id changed', async () => {
    const slow = deferred<ReturnType<typeof okResult>>()
    const fast = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise)

    const { result, rerender } = renderHook(({ id }) => usePetAd(id, true), {
      initialProps: { id: 'ad-1' },
    })
    rerender({ id: 'ad-2' })
    await act(async () => fast.resolve(okResult(adBody('New'))))
    await act(async () => slow.resolve(okResult(adBody('Old'))))

    const state = result.current.state
    expect(state.status).toBe('success')
    if (state.status === 'success') expect(state.ad.title).toBe('New')
  })

  it('does not crash when a response arrives after unmount', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(pending.promise)

    const { unmount } = renderHook(() => usePetAd('ad-1', true))
    unmount()

    await act(async () => pending.resolve(okResult(adBody())))

    expect(get).toHaveBeenCalledTimes(1)
  })

  it('asks again with the same id when retry is called', async () => {
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(adBody()))

    const { result } = renderHook(() => usePetAd('ad-1', true))
    await waitFor(() => expect(result.current.state.status).toBe('error'))

    act(() => result.current.retry())

    await waitFor(() => expect(result.current.state.status).toBe('success'))
    expect(get).toHaveBeenCalledTimes(2)
  })
})
