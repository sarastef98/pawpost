import { Link } from 'react-router-dom'
import Badge from '../../components/Badge'
import Card from '../../components/Card'
import { formatPrice } from './formatPrice'
import type { PetAdSummary } from './petAdListQuery'

type PetAdCardProps = {
  ad: PetAdSummary
  // A closed ad is shown as a plain card: a "Closed" badge and no link.
  closed?: boolean
}

export default function PetAdCard({ ad, closed = false }: PetAdCardProps) {
  const price = formatPrice(ad.type, ad.price)

  const card = (
    <Card className="flex h-full flex-col gap-2">
      <h3 className="text-lg break-words">{ad.title}</h3>
      <div className="flex flex-wrap gap-2">
        <Badge variant={ad.type === 'Sale' ? 'sale' : 'adoption'} />
        {closed && <Badge variant="closed" />}
      </div>
      {price && (
        <p className="text-base font-semibold break-words text-text">{price}</p>
      )}
      {ad.location && (
        <p className="text-sm break-words text-muted">{ad.location}</p>
      )}
      {ad.creator && (
        <p className="text-sm break-words text-muted">
          By {ad.creator.displayName}
        </p>
      )}
    </Card>
  )

  if (closed) return card

  return (
    <Link to={`/pet-ads/${encodeURIComponent(ad.id)}`} className="block">
      {card}
    </Link>
  )
}
