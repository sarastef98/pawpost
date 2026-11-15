import { useCallback, useEffect, useMemo, useState } from 'react'
import { client } from '../../api/client'
import { toPetAdDetails } from './petAdDetailsData'
import type { PetAdDetails } from './petAdDetailsData'

type Outcome =
  | { status: 'unauthorized' }
  | { status: 'notFound' }
  | { status: 'error' }
  | { status: 'success'; ad: PetAdDetails }

type Stored = {
  key: string
  outcome: Outcome
}

export type PetAdState = { status: 'loading' } | Outcome

// Loads one ad, but only when `enabled` is true (the visitor is logged in).
// "Loading" is derived: the stored result belongs to another request key than
// the current one. So an old ad is never shown for a new id, and a slow older
// response is ignored.
export function usePetAd(id: string, enabled: boolean) {
  const [attempt, setAttempt] = useState(0)
  const [stored, setStored] = useState<Stored | null>(null)

  const key = JSON.stringify([id, attempt])

  useEffect(() => {
    if (!enabled) return
    let cancelled = false

    async function load() {
      let outcome: Outcome = { status: 'error' }
      try {
        const result = await client.GET('/api/pet-ads/{id}', {
          params: { path: { id } },
        })
        const code = result.response.status
        if (result.response.ok && result.data !== undefined) {
          const ad = toPetAdDetails(result.data)
          if (ad !== null) outcome = { status: 'success', ad }
        } else if (code === 401) {
          outcome = { status: 'unauthorized' }
        } else if (code === 404 || code === 400) {
          // A bad id can never be fixed by trying again, so show "not found".
          outcome = { status: 'notFound' }
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
  }, [key, id, enabled])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  const state = useMemo<PetAdState>(() => {
    if (!enabled || stored === null || stored.key !== key) {
      return { status: 'loading' }
    }
    return stored.outcome
  }, [stored, key, enabled])

  return { state, retry }
}
