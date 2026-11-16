import { useCallback, useEffect, useMemo, useState } from 'react'
import { client } from '../../api/client'
import { PAGE_SIZE } from '../petAds/petAdListQuery'
import { toCreatorProfile } from './creatorProfileData'
import type { CreatorProfile } from './creatorProfileData'

type Outcome =
  | { status: 'notFound' }
  | { status: 'error' }
  | { status: 'success'; profile: CreatorProfile }

type Stored = {
  key: string
  outcome: Outcome
}

export type CreatorProfileState = { status: 'loading' } | Outcome

// Loads the public profile of one creator. No login is needed, so it always
// runs. "Loading" is derived: the stored result belongs to another request
// key than the current one. So an old page is never shown for a new page, and
// a slow older response is ignored.
export function useCreatorProfile(id: string, page: number) {
  const [attempt, setAttempt] = useState(0)
  const [stored, setStored] = useState<Stored | null>(null)

  const key = JSON.stringify([id, page, attempt])

  useEffect(() => {
    let cancelled = false

    async function load() {
      let outcome: Outcome = { status: 'error' }
      try {
        const result = await client.GET('/api/creators/{id}', {
          params: { path: { id }, query: { page, pageSize: PAGE_SIZE } },
        })
        const code = result.response.status
        if (result.response.ok && result.data !== undefined) {
          const profile = toCreatorProfile(result.data)
          if (profile !== null) outcome = { status: 'success', profile }
        } else if (code === 404 || code === 400) {
          // A bad id can never be fixed by trying again, so show "not found".
          // A 400 can only mean an invalid creator id, because page and
          // pageSize are validated on the client before the request.
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
  }, [key, id, page])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  const state = useMemo<CreatorProfileState>(() => {
    if (stored === null || stored.key !== key) return { status: 'loading' }
    return stored.outcome
  }, [stored, key])

  return { state, retry }
}
