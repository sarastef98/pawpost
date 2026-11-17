import { useEffect, useRef } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import Button from '../../components/Button'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import PetAdCard from '../petAds/PetAdCard'
import { totalPages } from '../petAds/petAdListQuery'
import { useMyClosedPetAds } from '../petAds/useMyClosedPetAds'
import { parseClosedAdsPage } from './creatorProfileData'

// Content of the "Closed ads" tab: the logged-in user's own closed ads, in the
// order the API returns them. It is only mounted for the owner when the tab is
// opened, so a new request is sent every time. The cards are plain: no link and
// no actions.
export default function ClosedAdsTab() {
  const [searchParams, setSearchParams] = useSearchParams()
  const page = parseClosedAdsPage(searchParams)
  const { state, retry } = useMyClosedPetAds(page)
  const panelRef = useRef<HTMLElement | null>(null)
  const pendingFocus = useRef(false)

  // A page beyond the last: go to the last page (or remove the key when there
  // are no closed ads). Replace the URL so Back does not loop.
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
        if (next > 1) params.set('closedAdsPage', String(next))
        else params.delete('closedAdsPage')
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
        if (lastPage > 1) params.set('closedAdsPage', String(lastPage))
        else params.delete('closedAdsPage')
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
    pendingFocus.current = true
    updatePage(next, false)
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
      {state.status === 'loading' || outOfRange ? (
        <Spinner />
      ) : state.status === 'error' ? (
        <ErrorMessage
          message="We could not load your closed ads. Please try again."
          onRetry={retryLoad}
        />
      ) : state.data.items.length === 0 ? (
        <EmptyState message="You have no closed ads." />
      ) : (
        <>
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {state.data.items.map((ad) => (
              <li key={ad.id}>
                <PetAdCard ad={ad} closed />
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
