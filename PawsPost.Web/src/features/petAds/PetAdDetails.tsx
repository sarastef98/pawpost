import type { Ref } from 'react'
import Badge from '../../components/Badge'
import Card from '../../components/Card'
import { formatPrice } from './formatPrice'
import { formatAge } from './petAdDetailsData'
import type { PetAdDetails as PetAdDetailsData } from './petAdDetailsData'

type PetAdDetailsProps = {
  ad: PetAdDetailsData
  headingRef?: Ref<HTMLHeadingElement>
}

type Row = { label: string; value: string }

export default function PetAdDetails({ ad, headingRef }: PetAdDetailsProps) {
  const price = formatPrice(ad.type, ad.price)
  const age = formatAge(ad.ageInMonths)
  // The title is required by the API. If it is empty anyway, use the species.
  const title = ad.title.trim() === '' ? ad.species : ad.title

  const rows: Row[] = [{ label: 'Species', value: ad.species }]
  if (ad.breed) rows.push({ label: 'Breed', value: ad.breed })
  if (age) rows.push({ label: 'Age', value: age })
  if (ad.location) rows.push({ label: 'Location', value: ad.location })

  return (
    <Card className="flex flex-col gap-4">
      <h1 ref={headingRef} tabIndex={-1} className="text-2xl break-words">
        {title}
      </h1>
      <div>
        <Badge variant={ad.type === 'Sale' ? 'sale' : 'adoption'} />
      </div>
      {price && (
        <p className="text-lg font-semibold break-words text-text">{price}</p>
      )}
      <dl className="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-[auto_1fr]">
        {rows.map((row) => (
          <div key={row.label} className="contents">
            <dt className="text-sm text-muted">{row.label}</dt>
            <dd className="mb-2 text-base break-words sm:mb-0">{row.value}</dd>
          </div>
        ))}
      </dl>
      <p className="text-base break-words whitespace-pre-line">
        {ad.description}
      </p>
    </Card>
  )
}
