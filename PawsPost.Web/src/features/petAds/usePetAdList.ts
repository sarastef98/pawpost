import { useCallback, useEffect, useState } from 'react'
import { client } from '../../api/client'
import { PAGE_SIZE, toListData } from './petAdListQuery'
import type { ListQuery, PetAdListData } from './petAdListQuery'

type Stored = {
  key: string
  // null means the request failed or the response was not usable
  data: PetAdListData | null
}

export type PetAdListState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'success'; data: PetAdListData }

// Loads one page of active ads. "Loading" is derived: the stored result
// belongs to another request key than the current one. So the old list is
// never shown for a new query, and a slow older response is ignored.
export function usePetAdList(query: ListQuery) {
  const { type, species, page } = query
  const [attempt, setAttempt] = useState(0)
  const [stored, setStored] = useState<Stored | null>(null)

  const key = JSON.stringify([type, species, page, attempt])

  useEffect(() => {
    let cancelled = false

    async function load() {
      let data: PetAdListData | null = null
      try {
        const result = await client.GET('/api/pet-ads', {
          params: {
            query: {
              page,
              pageSize: PAGE_SIZE,
              ...(type ? { type } : {}),
              ...(species ? { species } : {}),
            },
          },
        })
        if (result.response.ok && result.data !== undefined) {
          data = toListData(result.data)
        }
      } catch {
        data = null
      }
      if (!cancelled) setStored({ key, data })
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [key, type, species, page])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  let state: PetAdListState = { status: 'loading' }
  if (stored !== null && stored.key === key) {
    state =
      stored.data === null
        ? { status: 'error' }
        : { status: 'success', data: stored.data }
  }

  return { state, retry }
}
