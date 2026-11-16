import { act, renderHook, waitFor } from '@testing-library/react'
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
import { useCreatorProfile } from './useCreatorProfile'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const get = client.GET as unknown as Mock

function profile(name = 'Anna') {
  return creatorProfileBody(name, [sampleListItem()])
}

describe('useCreatorProfile', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('asks for the profile with the id, the page and the page size', async () => {
    get.mockResolvedValue(okResult(profile()))

    renderHook(() => useCreatorProfile('creator-1', 2))

    await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
    expect(get).toHaveBeenCalledWith('/api/creators/{id}', {
      params: { path: { id: 'creator-1' }, query: { page: 2, pageSize: 20 } },
    })
  })

  it('is loading first and then has the profile on 200', async () => {
    get.mockResolvedValue(okResult(profile()))

    const { result } = renderHook(() => useCreatorProfile('creator-1', 1))

    expect(result.current.state.status).toBe('loading')
    await waitFor(() => expect(result.current.state.status).toBe('success'))
    const state = result.current.state
    if (state.status === 'success') {
      expect(state.profile.displayName).toBe('Anna')
    }
  })

  it.each([
    [404, 'notFound'],
    [400, 'notFound'],
    [500, 'error'],
  ])('maps a %i answer to %s', async (code, expected) => {
    get.mockResolvedValue(failResult(code))

    const { result } = renderHook(() => useCreatorProfile('creator-1', 1))

    await waitFor(() => expect(result.current.state.status).toBe(expected))
  })

  it('reports an error when the client throws', async () => {
    get.mockRejectedValue(new Error('network'))

    const { result } = renderHook(() => useCreatorProfile('creator-1', 1))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('reports an error when the body has the wrong shape', async () => {
    get.mockResolvedValue(okResult({ id: 'creator-1' }))

    const { result } = renderHook(() => useCreatorProfile('creator-1', 1))

    await waitFor(() => expect(result.current.state.status).toBe('error'))
  })

  it('is loading again for a new page and does not keep the old one', async () => {
    get.mockResolvedValueOnce(okResult(profile('First')))
    const next = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(next.promise)

    const { result, rerender } = renderHook(
      ({ page }) => useCreatorProfile('creator-1', page),
      { initialProps: { page: 1 } },
    )
    await waitFor(() => expect(result.current.state.status).toBe('success'))

    rerender({ page: 2 })

    expect(result.current.state.status).toBe('loading')
    await act(async () => next.resolve(okResult(profile('Second'))))
    await waitFor(() => expect(result.current.state.status).toBe('success'))
  })

  it('ignores a slow response for an old page', async () => {
    const slow = deferred<ReturnType<typeof okResult>>()
    const fast = deferred<ReturnType<typeof okResult>>()
    get.mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise)

    const { result, rerender } = renderHook(
      ({ page }) => useCreatorProfile('creator-1', page),
      { initialProps: { page: 1 } },
    )
    rerender({ page: 2 })
    await act(async () => fast.resolve(okResult(profile('New'))))
    await act(async () => slow.resolve(okResult(profile('Old'))))

    const state = result.current.state
    expect(state.status).toBe('success')
    if (state.status === 'success') {
      expect(state.profile.displayName).toBe('New')
    }
  })

  it('sends a new request when retry is called', async () => {
    get.mockResolvedValueOnce(failResult(500))
    get.mockResolvedValueOnce(okResult(profile()))

    const { result } = renderHook(() => useCreatorProfile('creator-1', 1))
    await waitFor(() => expect(result.current.state.status).toBe('error'))

    act(() => result.current.retry())

    await waitFor(() => expect(result.current.state.status).toBe('success'))
    expect(get).toHaveBeenCalledTimes(2)
  })
})
