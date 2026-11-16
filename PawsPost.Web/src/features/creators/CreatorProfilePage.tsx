import { useEffect, useRef } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import { totalPages } from '../petAds/petAdListQuery'
import CreatorAdList from './CreatorAdList'
import { parseProfilePage } from './creatorProfileData'
import { useCreatorProfile } from './useCreatorProfile'

const linkClasses = 'font-semibold text-primary underline'

// Public page: no login is needed and the login state is not read.
export default function CreatorProfilePage() {
  const { id = '' } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const page = parseProfilePage(searchParams)
  const { state, retry } = useCreatorProfile(id, page)

  const headingRef = useRef<HTMLHeadingElement>(null)
  const adsHeadingRef = useRef<HTMLHeadingElement>(null)
  const pendingFocus = useRef(false)

  // A page beyond the last: go to the last page (or page 1 when there are no
  // ads at all). Replace the URL so Back does not loop.
  const pages =
    state.status === 'success'
      ? totalPages(state.profile.ads.totalCount, state.profile.ads.pageSize)
      : 0
  const outOfRange =
    state.status === 'success' &&
    state.profile.ads.items.length === 0 &&
    page > Math.max(pages, 1)

  useEffect(() => {
    if (!outOfRange) return
    const lastPage = Math.max(pages, 1)
    setSearchParams(
      lastPage > 1 ? { page: String(lastPage) } : new URLSearchParams(),
      { replace: true },
    )
  }, [outOfRange, pages, setSearchParams])

  // The pager and Try again buttons are replaced while loading, so focus goes
  // to a heading once the new result has settled.
  useEffect(() => {
    if (state.status === 'loading' || outOfRange || !pendingFocus.current) {
      return
    }
    pendingFocus.current = false
    const target =
      state.status === 'success' ? adsHeadingRef.current : headingRef.current
    target?.focus()
    target?.scrollIntoView?.()
  }, [state, outOfRange])

  function goToPage(next: number) {
    pendingFocus.current = true
    setSearchParams(next > 1 ? { page: String(next) } : new URLSearchParams())
  }

  function retryLoad() {
    pendingFocus.current = true
    retry()
  }

  function renderContent() {
    if (state.status === 'loading' || outOfRange) {
      return (
        <>
          <h1 className="text-2xl">Creator profile</h1>
          <Spinner />
        </>
      )
    }
    if (state.status === 'success') {
      return (
        <>
          <h1 className="text-2xl break-words">{state.profile.displayName}</h1>
          <CreatorAdList
            displayName={state.profile.displayName}
            ads={state.profile.ads}
            page={page}
            onPageChange={goToPage}
            headingRef={adsHeadingRef}
          />
        </>
      )
    }
    return (
      <>
        <h1 ref={headingRef} tabIndex={-1} className="text-2xl">
          Creator profile
        </h1>
        {state.status === 'notFound' ? (
          <EmptyState message="We could not find this person.">
            <Link to="/" className={linkClasses}>
              Go to the home page
            </Link>
          </EmptyState>
        ) : (
          <ErrorMessage
            message="We could not load this profile. Please try again."
            onRetry={retryLoad}
          />
        )}
      </>
    )
  }

  return (
    <section className="flex flex-col gap-4">
      <Link to="/" className={linkClasses}>
        Back to all ads
      </Link>
      {renderContent()}
    </section>
  )
}
