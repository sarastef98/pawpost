import { useEffect, useRef } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Notice from '../../components/Notice'
import Spinner from '../../components/Spinner'
import { useAuth } from '../auth/useAuth'
import ClosePetAdButton from './ClosePetAdButton'
import PetAdDetails from './PetAdDetails'
import { usePetAd } from './usePetAd'

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
  const { status, user } = useAuth()
  const navigate = useNavigate()
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

  function reloadAd() {
    pendingFocus.current = true
    retry()
  }

  function goHome() {
    void navigate('/', {
      replace: true,
      state: { notice: 'Your ad was closed.' },
    })
  }

  if (state.status === 'unauthorized') return <Navigate to="/login" replace />
  if (state.status === 'success') {
    return (
      <>
        <Notice />
        <PetAdDetails ad={state.ad} headingRef={headingRef} />
        {isOwner(status, user?.id, state.ad.creator?.id) && (
          <div>
            <ClosePetAdButton
              adId={state.ad.id}
              onClosed={goHome}
              onGone={reloadAd}
            />
          </div>
        )}
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

// Only decides what is shown. The server still decides (403). The ids are
// compared without case, like on the creator profile page.
function isOwner(
  status: string,
  userId: string | undefined,
  creatorId: string | undefined,
): boolean {
  return (
    status === 'loggedIn' &&
    userId !== undefined &&
    creatorId !== undefined &&
    userId.toLowerCase() === creatorId.toLowerCase()
  )
}
