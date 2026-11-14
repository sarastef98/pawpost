import { Link } from 'react-router-dom'
import Badge from '../../components/Badge'
import Card from '../../components/Card'
import { formatPrice } from './formatPrice'
import type { PetAdSummary } from './petAdListQuery'

type PetAdCardProps = {
  ad: PetAdSummary
}

export default function PetAdCard({ ad }: PetAdCardProps) {
  const price = formatPrice(ad.type, ad.price)

  return (
    <Link to={`/pet-ads/${encodeURIComponent(ad.id)}`} className="block">
      <Card className="flex h-full flex-col gap-2">
        <h3 className="text-lg break-words">{ad.title}</h3>
        <div>
          <Badge variant={ad.type === 'Sale' ? 'sale' : 'adoption'} />
        </div>
        {price && (
          <p className="text-base font-semibold break-words text-text">
            {price}
          </p>
        )}
        {ad.location && (
          <p className="text-sm break-words text-muted">{ad.location}</p>
        )}
      </Card>
    </Link>
  )
}
