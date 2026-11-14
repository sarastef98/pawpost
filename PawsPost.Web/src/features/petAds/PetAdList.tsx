import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import Button from '../../components/Button'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import PetAdCard from './PetAdCard'
import PetAdFilters from './PetAdFilters'
import { buildSearchParams, parseListQuery, totalPages } from './petAdListQuery'
import type { AdTypeFilter } from './petAdListQuery'
import { usePetAdList } from './usePetAdList'

type PendingFocus = 'heading' | 'type' | null

export default function PetAdList() {
  const [searchParams, setSearchParams] = useSearchParams()
  const query = parseListQuery(searchParams)
  const { type, species, page } = query
  const { state, retry } = usePetAdList(query)

  const headingRef = useRef<HTMLHeadingElement>(null)
  const typeRef = useRef<HTMLSelectElement>(null)
  const pendingFocus = useRef<PendingFocus>(null)

  // A page beyond the last: go to the last page (or page 1 when there are no
  // ads at all). Replace the URL so Back does not loop.
  const pages =
    state.status === 'success'
      ? totalPages(state.data.totalCount, state.data.pageSize)
      : 0
  const outOfRange =
    state.status === 'success' &&
    state.data.items.length === 0 &&
    page > Math.max(pages, 1)

  useEffect(() => {
    if (!outOfRange) return
    setSearchParams(
      buildSearchParams({ type, species, page: Math.max(pages, 1) }),
      {
        replace: true,
      },
    )
  }, [outOfRange, pages, type, species, page, setSearchParams])

  // Move focus once the new results are shown, so the keyboard user does not
  // lose their place when the pager or the Clear button is replaced.
  useEffect(() => {
    if (state.status === 'loading' || outOfRange) return
    if (pendingFocus.current === 'heading') {
      headingRef.current?.focus()
      headingRef.current?.scrollIntoView?.()
    } else if (pendingFocus.current === 'type') {
      typeRef.current?.focus()
    }
    pendingFocus.current = null
  }, [state, outOfRange])

  function goToPage(next: number) {
    pendingFocus.current = 'heading'
    setSearchParams(buildSearchParams({ ...query, page: next }))
  }

  function applyFilters(nextType: AdTypeFilter, nextSpecies: string) {
    setSearchParams(
      buildSearchParams({ type: nextType, species: nextSpecies, page: 1 }),
    )
  }

  function clearFilters() {
    pendingFocus.current = 'type'
    setSearchParams(new URLSearchParams())
  }

  const hasFilters = type !== '' || species !== ''

  function renderResults() {
    if (state.status === 'loading' || outOfRange) {
      return <Spinner />
    }
    if (state.status === 'error') {
      return (
        <ErrorMessage
          message="We could not load the ads. Please try again."
          onRetry={retry}
        />
      )
    }
    const { items, pageSize, totalCount } = state.data
    if (items.length === 0) {
      return hasFilters ? (
        <EmptyState message="No ads match your filters.">
          <Button variant="secondary" onClick={clearFilters}>
            Clear filters
          </Button>
        </EmptyState>
      ) : (
        <EmptyState message="There are no ads yet. Please check back soon." />
      )
    }
    const lastPage = totalPages(totalCount, pageSize)
    return (
      <>
        <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((ad) => (
            <li key={ad.id}>
              <PetAdCard ad={ad} />
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
            Page {page} of {lastPage}
          </span>
          <Button
            variant="secondary"
            disabled={page >= lastPage}
            onClick={() => goToPage(page + 1)}
          >
            Next
          </Button>
        </nav>
      </>
    )
  }

  // The live region keeps its last text while the next page loads, so paging
  // with the same count announces nothing new. When the filters change it is
  // emptied while loading, so the new count is announced even when it is the
  // same number. It only gets new text once a result is settled.
  let settledSummary: string | null = null
  if (state.status !== 'loading' && !outOfRange) {
    settledSummary = ''
    if (state.status === 'success' && state.data.items.length > 0) {
      const count = state.data.totalCount
      settledSummary = `${count} ${count === 1 ? 'ad' : 'ads'} found`
    }
  }
  const filterKey = JSON.stringify([type, species])
  const [announced, setAnnounced] = useState({ text: '', filterKey })
  if (
    settledSummary !== null &&
    (settledSummary !== announced.text || filterKey !== announced.filterKey)
  ) {
    setAnnounced({ text: settledSummary, filterKey })
  }
  const summary =
    settledSummary ?? (announced.filterKey === filterKey ? announced.text : '')

  return (
    <section className="flex flex-col gap-4">
      <h2 ref={headingRef} tabIndex={-1} className="text-xl">
        Latest ads
      </h2>
      <PetAdFilters
        type={type}
        species={species}
        onApply={applyFilters}
        typeRef={typeRef}
      />
      <p aria-live="polite" className="text-sm text-muted">
        {summary}
      </p>
      {renderResults()}
    </section>
  )
}
