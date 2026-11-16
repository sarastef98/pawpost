import type { components } from '../../api/schema'
import { toFiniteNumber } from './formatPrice'
import { toPublicCreator } from './petAdCreator'
import type { PublicCreator } from './petAdCreator'

type Response = components['schemas']['PetAdDetailsResponse']

// The fields the page shows. The API sends numbers as number or string, and
// breed, price and location may be null, so they are converted here.
export type PetAdDetails = Pick<Response, 'id' | 'title' | 'description'> & {
  species: string
  type: string
  breed: string | null
  ageInMonths: number | null
  price: number | null
  location: string | null
  creator: PublicCreator | null
}

function toText(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function toOptionalText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null
}

// Converts an API response into safe values. Returns null when a required
// field is missing or has the wrong type (the UI shows the error state).
export function toPetAdDetails(value: unknown): PetAdDetails | null {
  if (typeof value !== 'object' || value === null) return null
  const data = value as Record<string, unknown>
  if (typeof data.id !== 'string' || data.id === '') return null
  if (typeof data.title !== 'string') return null
  if (typeof data.species !== 'string') return null
  if (typeof data.description !== 'string') return null

  const age = toFiniteNumber(data.ageInMonths)
  return {
    id: data.id,
    title: data.title,
    type: toText(data.type),
    species: data.species,
    breed: toOptionalText(data.breed),
    ageInMonths: age !== null && Number.isInteger(age) && age >= 0 ? age : null,
    description: data.description,
    price: toFiniteNumber(data.price),
    location: toOptionalText(data.location),
    creator: toPublicCreator(data.creator),
  }
}

function plural(count: number, unit: string): string {
  return `${count} ${unit}${count === 1 ? '' : 's'}`
}

// Age text such as "3 months", "1 year" or "2 years 3 months". Invalid input
// gives null (show nothing).
export function formatAge(months: unknown): string | null {
  const total = toFiniteNumber(months)
  if (total === null || !Number.isInteger(total) || total < 0) return null
  const years = Math.floor(total / 12)
  const rest = total % 12
  if (years === 0) return plural(rest, 'month')
  if (rest === 0) return plural(years, 'year')
  return `${plural(years, 'year')} ${plural(rest, 'month')}`
}
