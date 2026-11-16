import { useEffect, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

function readNotice(state: unknown): string | null {
  if (
    typeof state === 'object' &&
    state !== null &&
    'notice' in state &&
    typeof state.notice === 'string'
  ) {
    return state.notice
  }
  return null
}

// A short message passed through router state, for example after posting an
// ad. The message is read once and the router state is then cleared, so a
// reload or a later visit to this history entry does not show it again.
// The empty region renders first and the text is filled in on the next tick,
// because screen readers do not reliably announce a live region that is
// inserted with its text already in it.
export default function Notice() {
  const location = useLocation()
  const navigate = useNavigate()
  const [notice] = useState(() => readNotice(location.state))
  const [shown, setShown] = useState('')
  const { pathname, search, hash } = location

  useEffect(() => {
    if (notice === null) return
    void navigate({ pathname, search, hash }, { replace: true, state: null })
  }, [notice, navigate, pathname, search, hash])

  useEffect(() => {
    const timer = setTimeout(() => setShown(notice ?? ''), 0)
    return () => clearTimeout(timer)
  }, [notice])

  return (
    <p role="status" className={shown ? 'text-base text-success' : 'sr-only'}>
      {shown}
    </p>
  )
}
