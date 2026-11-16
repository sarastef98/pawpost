import { useCallback, useEffect, useRef, useState } from 'react'
import { client } from '../../api/client'

export type CloseState =
  | 'idle'
  | 'pending'
  | 'closed'
  | 'unauthorized'
  | 'forbidden'
  | 'gone'
  | 'error'

// Sends the close request for one ad. Only one request runs at a time: the
// in-flight flag is set synchronously, so a double click sends one request.
// The response body is never read or shown, only the status code is mapped.
// If the user leaves the page while the request runs, the result is dropped:
// the ad may be closed on the server, but the state never becomes 'closed',
// so onClosed never runs (no message, no reload). This is on purpose: a
// late answer must not change or navigate a page the user has already left.
export function useClosePetAd() {
  const [state, setState] = useState<CloseState>('idle')
  const inFlight = useRef(false)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  const close = useCallback(async (id: string) => {
    if (inFlight.current) return
    inFlight.current = true
    setState('pending')
    let outcome: CloseState = 'error'
    try {
      const result = await client.POST('/api/pet-ads/{id}/close', {
        params: { path: { id } },
      })
      const code = result.response.status
      if (result.response.ok) outcome = 'closed'
      else if (code === 401) outcome = 'unauthorized'
      else if (code === 403) outcome = 'forbidden'
      else if (code === 404 || code === 409 || code === 400) outcome = 'gone'
    } catch {
      outcome = 'error'
    }
    inFlight.current = false
    if (mounted.current) setState(outcome)
  }, [])

  const reset = useCallback(() => setState('idle'), [])

  return { state, close, reset }
}
