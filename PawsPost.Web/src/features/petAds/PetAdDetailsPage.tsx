import { useEffect, useRef, useState } from 'react'
import {
  Link,
  Navigate,
  useLocation,
  useNavigate,
  useParams,
} from 'react-router-dom'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import { useAuth } from '../auth/useAuth'
import PetAdDetails from './PetAdDetails'
import { usePetAd } from './usePetAd'

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
function Notice() {
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

const linkClasses = 'font-semibold text-primary underline'

function BackLink() {
  return (
    <Link to="/" className={linkClasses}>
      Back to all ads
    </Link>
  )
}

// The guard. The ad is only requested by the inner component, which is only
// rendered once the visitor is known to be logged in.
export default function PetAdDetailsPage() {
  const { status } = useAuth()
  const { id = '' } = useParams()

  if (status === 'loggedOut') return <Navigate to="/login" replace />

  return (
    <section className="flex flex-col gap-4">
      <BackLink />
      {status === 'loading' ? (
        <>
          <h1 className="text-2xl">Ad details</h1>
          <Spinner />
        </>
      ) : (
        <LoadedAd id={id} />
      )}
    </section>
  )
}

function LoadedAd({ id }: { id: string }) {
  const { state, retry } = usePetAd(id, true)
  const headingRef = useRef<HTMLHeadingElement>(null)
  const pendingFocus = useRef(false)

  // The Try again button is replaced by the spinner, so focus goes to the
  // heading once the result settles.
  useEffect(() => {
    if (state.status === 'loading' || !pendingFocus.current) return
    pendingFocus.current = false
    headingRef.current?.focus()
  }, [state])

  function retryLoad() {
    pendingFocus.current = true
    retry()
  }

  if (state.status === 'unauthorized') return <Navigate to="/login" replace />
  if (state.status === 'success') {
    return (
      <>
        <Notice />
        <PetAdDetails ad={state.ad} headingRef={headingRef} />
      </>
    )
  }

  return (
    <>
      <h1 ref={headingRef} tabIndex={-1} className="text-2xl">
        Ad details
      </h1>
      {state.status === 'loading' && <Spinner />}
      {state.status === 'notFound' && (
        <EmptyState message="We could not find this ad. It may have been removed.">
          <Link to="/" className={linkClasses}>
            Go to the home page
          </Link>
        </EmptyState>
      )}
      {state.status === 'error' && (
        <ErrorMessage
          message="We could not load this ad. Please try again."
          onRetry={retryLoad}
        />
      )}
    </>
  )
}
