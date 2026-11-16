import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, noContentResult } from '../../test/apiResponses'
import { useClosePetAd } from './useClosePetAd'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const post = client.POST as unknown as Mock

describe('useClosePetAd', () => {
  beforeEach(() => {
    post.mockReset()
  })

  it('starts idle and sends nothing', () => {
    const { result } = renderHook(() => useClosePetAd())

    expect(result.current.state).toBe('idle')
    expect(post).not.toHaveBeenCalled()
  })

  it('posts to the close route with the id in the path and no body', async () => {
    post.mockResolvedValue(noContentResult())
    const { result } = renderHook(() => useClosePetAd())

    await act(() => result.current.close('ad-7'))

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/pet-ads/{id}/close', {
      params: { path: { id: 'ad-7' } },
    })
  })

  it('reports closed on 204', async () => {
    post.mockResolvedValue(noContentResult())
    const { result } = renderHook(() => useClosePetAd())

    await act(() => result.current.close('ad-1'))

    expect(result.current.state).toBe('closed')
  })

  it.each([
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'gone'],
    [409, 'gone'],
    [400, 'gone'],
    [500, 'error'],
  ])('maps a %i answer to %s', async (code, expected) => {
    post.mockResolvedValue(failResult(code))
    const { result } = renderHook(() => useClosePetAd())

    await act(() => result.current.close('ad-1'))

    expect(result.current.state).toBe(expected)
  })

  it('reports error when the request is rejected', async () => {
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    const { result } = renderHook(() => useClosePetAd())

    await act(() => result.current.close('ad-1'))

    expect(result.current.state).toBe('error')
  })

  it('is pending while the request runs', async () => {
    const pending = deferred<ReturnType<typeof noContentResult>>()
    post.mockReturnValue(pending.promise)
    const { result } = renderHook(() => useClosePetAd())

    act(() => {
      void result.current.close('ad-1')
    })
    expect(result.current.state).toBe('pending')

    await act(async () => pending.resolve(noContentResult()))
    await waitFor(() => expect(result.current.state).toBe('closed'), {
      timeout: 5_000,
    })
  })

  it('sends no second request while the first is pending', async () => {
    const pending = deferred<ReturnType<typeof noContentResult>>()
    post.mockReturnValue(pending.promise)
    const { result } = renderHook(() => useClosePetAd())

    act(() => {
      void result.current.close('ad-1')
      void result.current.close('ad-1')
    })
    await act(async () => pending.resolve(noContentResult()))

    expect(post).toHaveBeenCalledTimes(1)
  })

  it('allows a new request after the first one failed', async () => {
    post.mockResolvedValueOnce(failResult(500))
    post.mockResolvedValueOnce(noContentResult())
    const { result } = renderHook(() => useClosePetAd())

    await act(() => result.current.close('ad-1'))
    await act(() => result.current.close('ad-1'))

    expect(post).toHaveBeenCalledTimes(2)
    expect(result.current.state).toBe('closed')
  })

  it('goes back to idle on reset', async () => {
    post.mockResolvedValue(failResult(403))
    const { result } = renderHook(() => useClosePetAd())
    await act(() => result.current.close('ad-1'))

    act(() => result.current.reset())

    expect(result.current.state).toBe('idle')
  })

  it('does not crash when the answer arrives after unmount', async () => {
    const pending = deferred<ReturnType<typeof noContentResult>>()
    post.mockReturnValue(pending.promise)
    const { result, unmount } = renderHook(() => useClosePetAd())
    act(() => {
      void result.current.close('ad-1')
    })

    unmount()
    await act(async () => pending.resolve(noContentResult()))

    expect(post).toHaveBeenCalledTimes(1)
  })
})
