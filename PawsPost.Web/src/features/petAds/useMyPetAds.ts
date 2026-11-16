import { useCallback, useEffect, useMemo, useState } from 'react'
import { client } from '../../api/client'
import { PAGE_SIZE, toListData } from './petAdListQuery'
import type { PetAdListData } from './petAdListQuery'

type Outcome =
  | { status: 'unauthorized' }
  | { status: 'error' }
  | { status: 'success'; data: PetAdListData }

type Stored = {
  key: string
  outcome: Outcome
}

export type MyPetAdsState = { status: 'loading' } | Outcome

// Loads the logged-in user's own active ads, but only when `enabled` is true.
// "Loading" is derived: the stored result belongs to another request key than
// the current one. So an old page is never shown for a new page, and a slow
// older response is ignored.
export function useMyPetAds(page: number, enabled: boolean) {
  const [attempt, setAttempt] = useState(0)
  const [stored, setStored] = useState<Stored | null>(null)

  const key = JSON.stringify([page, attempt])

  useEffect(() => {
    if (!enabled) return
    let cancelled = false

    async function load() {
      let outcome: Outcome = { status: 'error' }
      try {
        const result = await client.GET('/api/pet-ads/mine', {
          params: { query: { page, pageSize: PAGE_SIZE } },
        })
        const code = result.response.status
        if (result.response.ok && result.data !== undefined) {
          const data = toListData(result.data)
          if (data !== null) outcome = { status: 'success', data }
        } else if (code === 401) {
          outcome = { status: 'unauthorized' }
        }
      } catch {
        outcome = { status: 'error' }
      }
      if (!cancelled) setStored({ key, outcome })
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [key, page, enabled])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  const state = useMemo<MyPetAdsState>(() => {
    if (stored === null || stored.key !== key) return { status: 'loading' }
    return stored.outcome
  }, [stored, key])

  return { state, retry }
}
