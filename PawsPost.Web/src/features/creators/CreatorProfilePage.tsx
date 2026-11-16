import { useEffect, useRef } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import { totalPages } from '../petAds/petAdListQuery'
import Tabs from '../../components/Tabs'
import { useAuth } from '../auth/useAuth'
import CreatorAdList from './CreatorAdList'
import MyAdsTab from './MyAdsTab'
import { parseProfilePage } from './creatorProfileData'
import { useCreatorProfile } from './useCreatorProfile'

const linkClasses = 'font-semibold text-primary underline'

// Public page: no login is needed. The login state only decides whether the
// owner sees their own tabs instead of the public ads list.
export default function CreatorProfilePage() {
  const { id = '' } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const page = parseProfilePage(searchParams)
  const { state, retry } = useCreatorProfile(id, page)
  const { status, user } = useAuth()
  // Only decides what is shown. The server decides what /mine returns.
  const isOwnProfile =
    status === 'loggedIn' &&
    user !== null &&
    user.id.toLowerCase() === id.toLowerCase()

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
      (current) => {
        const params = new URLSearchParams(current)
        if (lastPage > 1) params.set('page', String(lastPage))
        else params.delete('page')
        return params
      },
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
    setSearchParams((current) => {
      const params = new URLSearchParams(current)
      if (next > 1) params.set('page', String(next))
      else params.delete('page')
      return params
    })
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
          {isOwnProfile ? (
            <Tabs
              label="My profile"
              items={[
                { id: 'my-ads', label: 'My ads', content: () => <MyAdsTab /> },
              ]}
            />
          ) : (
            <CreatorAdList
              displayName={state.profile.displayName}
              ads={state.profile.ads}
              page={page}
              onPageChange={goToPage}
              headingRef={adsHeadingRef}
            />
          )}
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
