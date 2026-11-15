import { Link } from 'react-router-dom'
import EmptyState from '../../components/EmptyState'

export default function NotFoundPage() {
  return (
    <div className="flex flex-col gap-6">
      <title>Page not found</title>
      <h1 className="text-2xl">Page not found</h1>
      <EmptyState message="We could not find that page. Let's get you back to the ads.">
        <Link
          to="/"
          className="inline-flex min-h-11 items-center justify-center rounded-card bg-primary px-4 py-2 font-semibold text-surface hover:bg-primary-hover"
        >
          Back to home
        </Link>
      </EmptyState>
    </div>
  )
}
