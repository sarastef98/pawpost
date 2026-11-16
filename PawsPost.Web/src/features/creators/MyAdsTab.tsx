import { useEffect, useRef, useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import Button from '../../components/Button'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import PetAdCard from '../petAds/PetAdCard'
import ClosePetAdButton from '../petAds/ClosePetAdButton'
import { totalPages } from '../petAds/petAdListQuery'
import { useMyPetAds } from '../petAds/useMyPetAds'
import { parseMyAdsPage } from './creatorProfileData'

// Content of the "My ads" tab: the logged-in user's own active ads. It is only
// mounted for the owner, so the request is always wanted.
export default function MyAdsTab() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = parseMyAdsPage(searchParams)
  const { state, retry } = useMyPetAds(page, true)
  const panelRef = useRef<HTMLElement | null>(null)
  const pendingFocus = useRef(false)
  const [message, setMessage] = useState('')

  // A page beyond the last: go to the last page (or remove the key when there
  // are no ads). Replace the URL so Back does not loop.
  const pages =
    state.status === 'success'
      ? totalPages(state.data.totalCount, state.data.pageSize)
      : 0
  const outOfRange =
    state.status === 'success' &&
    state.data.items.length === 0 &&
    page > Math.max(pages, 1)

  function updatePage(next: number, replace: boolean) {
    setSearchParams(
      (current) => {
        const params = new URLSearchParams(current)
        if (next > 1) params.set('myAdsPage', String(next))
        else params.delete('myAdsPage')
        return params
      },
      { replace },
    )
  }

  useEffect(() => {
    if (!outOfRange) return
    setSearchParams(
      (current) => {
        const params = new URLSearchParams(current)
        const lastPage = Math.max(pages, 1)
        if (lastPage > 1) params.set('myAdsPage', String(lastPage))
        else params.delete('myAdsPage')
        return params
      },
      { replace: true },
    )
  }, [outOfRange, pages, setSearchParams])

  // The pager and Try again buttons are replaced while loading, so focus goes
  // to the tab panel once the new result has settled.
  useEffect(() => {
    if (state.status === 'loading' || outOfRange || !pendingFocus.current) {
      return
    }
    pendingFocus.current = false
    const panel = panelRef.current?.closest<HTMLElement>('[role="tabpanel"]')
    panel?.focus()
    panel?.scrollIntoView?.()
  }, [state, outOfRange])

  function goToPage(next: number) {
    setMessage('')
    pendingFocus.current = true
    updatePage(next, false)
  }

  function handleClosed() {
    setMessage('Your ad was closed.')
    pendingFocus.current = true
    retry()
  }

  function reloadList() {
    pendingFocus.current = true
    retry()
  }

  function retryLoad() {
    pendingFocus.current = true
    retry()
  }

  if (state.status === 'unauthorized') {
    return <Navigate to="/login" replace />
  }

  return (
    <section ref={panelRef} className="flex flex-col gap-4">
      <p
        role="status"
        className={message ? 'text-base text-success' : 'sr-only'}
      >
        {message}
      </p>
      {state.status === 'loading' || outOfRange ? (
        <Spinner />
      ) : state.status === 'error' ? (
        <ErrorMessage
          message="We could not load your ads. Please try again."
          onRetry={retryLoad}
        />
      ) : state.data.items.length === 0 ? (
        <EmptyState message="You have no active ads right now.">
          <Link
            to="/pet-ads/new"
            className="font-semibold text-primary underline"
          >
            Post an ad
          </Link>
        </EmptyState>
      ) : (
        <>
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {state.data.items.map((ad) => (
              <li key={ad.id} className="flex flex-col gap-2">
                <PetAdCard ad={ad} />
                <ClosePetAdButton
                  adId={ad.id}
                  adTitle={ad.title}
                  className="w-full self-start sm:w-auto"
                  onClosed={handleClosed}
                  onGone={reloadList}
                />
              </li>
            ))}
          </ul>
          <nav
            aria-label="Pages"
            className="flex items-center justify-between gap-4"
          >
            <Button
              variant="secondary"
              disabled={page <= 1}
              onClick={() => goToPage(page - 1)}
            >
              Previous
            </Button>
            <span className="text-base text-muted">
              Page {page} of {pages}
            </span>
            <Button
              variant="secondary"
              disabled={page >= pages}
              onClick={() => goToPage(page + 1)}
            >
              Next
            </Button>
          </nav>
        </>
      )}
    </section>
  )
}
