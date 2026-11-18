type BadgeVariant = 'adoption' | 'sale' | 'closed'

type BadgeProps = {
  variant: BadgeVariant
}

const variants: Record<BadgeVariant, { label: string; classes: string }> = {
  adoption: { label: 'Adoption', classes: 'bg-success text-surface' },
  sale: { label: 'Sale', classes: 'bg-primary-soft text-primary-hover' },
  closed: { label: 'Closed', classes: 'bg-border text-text' },
}

export default function Badge({ variant }: BadgeProps) {
  const { label, classes } = variants[variant]
  return (
    <span
      className={`inline-block rounded-pill px-3 py-1 text-sm font-semibold ${classes}`}
    >
      {label}
    </span>
  )
}
