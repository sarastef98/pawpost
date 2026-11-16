import type { Ref } from 'react'
import { Link } from 'react-router-dom'
import Button from '../../components/Button'
import EmptyState from '../../components/EmptyState'
import PetAdCard from '../petAds/PetAdCard'
import { totalPages } from '../petAds/petAdListQuery'
import type { PetAdListData } from '../petAds/petAdListQuery'

type CreatorAdListProps = {
  displayName: string
  ads: PetAdListData
  page: number
  onPageChange: (page: number) => void
  headingRef?: Ref<HTMLHeadingElement>
}

export default function CreatorAdList({
  displayName,
  ads,
  page,
  onPageChange,
  headingRef,
}: CreatorAdListProps) {
  const { items, totalCount, pageSize } = ads
  const lastPage = totalPages(totalCount, pageSize)

  return (
    <section className="flex flex-col gap-4">
      <h2 ref={headingRef} tabIndex={-1} className="text-xl">
        Ads
      </h2>
      {items.length === 0 ? (
        <EmptyState
          message={`${displayName} has no ads right now. Please check back soon.`}
        >
          <Link to="/" className="font-semibold text-primary underline">
            Browse all ads
          </Link>
        </EmptyState>
      ) : (
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
              onClick={() => onPageChange(page - 1)}
            >
              Previous
            </Button>
            <span className="text-base text-muted">
              Page {page} of {lastPage}
            </span>
            <Button
              variant="secondary"
              disabled={page >= lastPage}
              onClick={() => onPageChange(page + 1)}
            >
              Next
            </Button>
          </nav>
        </>
      )}
    </section>
  )
}
