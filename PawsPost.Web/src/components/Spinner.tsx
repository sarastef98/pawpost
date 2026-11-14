export default function Spinner() {
  return (
    <div role="status" className="inline-flex items-center">
      <span
        aria-hidden="true"
        className="size-8 animate-spin rounded-pill border-4 border-border border-t-primary"
      />
      <span className="sr-only">Loading</span>
    </div>
  )
}
