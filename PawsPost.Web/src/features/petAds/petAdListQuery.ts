import { toFiniteNumber } from './formatPrice'
import { toPublicCreator } from './petAdCreator'
import type { PublicCreator } from './petAdCreator'

export const PAGE_SIZE = 20
export const MAX_SPECIES_LENGTH = 50
const MAX_PAGE = 2147483647

export type AdTypeFilter = '' | 'Adoption' | 'Sale'

export type ListQuery = {
  type: AdTypeFilter
  species: string
  page: number
}

export type PetAdSummary = {
  id: string
  title: string
  type: string
  price: number | null
  location: string | null
  creator: PublicCreator | null
}

export type PetAdListData = {
  items: PetAdSummary[]
  totalCount: number
  page: number
  pageSize: number
}

// Reads the page state from the URL. Invalid values are ignored.
export function parseListQuery(params: URLSearchParams): ListQuery {
  const rawType = params.get('type')
  const type: AdTypeFilter =
    rawType === 'Adoption' || rawType === 'Sale' ? rawType : ''

  const rawSpecies = (params.get('species') ?? '').trim()
  const species = rawSpecies.length <= MAX_SPECIES_LENGTH ? rawSpecies : ''

  const rawPage = params.get('page') ?? ''
  let page = 1
  if (/^[1-9][0-9]*$/.test(rawPage)) {
    const parsed = Number(rawPage)
    if (Number.isSafeInteger(parsed) && parsed <= MAX_PAGE) page = parsed
  }

  return { type, species, page }
}

// Builds the URL query string. Empty filters and page 1 are left out.
export function buildSearchParams(query: ListQuery): URLSearchParams {
  const params = new URLSearchParams()
  if (query.type) params.set('type', query.type)
  const species = query.species.trim()
  if (species) params.set('species', species)
  if (query.page > 1) params.set('page', String(query.page))
  return params
}

export function totalPages(totalCount: number, pageSize: number): number {
  if (totalCount <= 0 || pageSize <= 0) return 0
  return Math.ceil(totalCount / pageSize)
}

function toInteger(value: unknown, min: number): number | null {
  const converted = toFiniteNumber(value)
  if (converted === null || !Number.isInteger(converted) || converted < min) {
    return null
  }
  return converted
}

function toItem(value: unknown): PetAdSummary | null {
  if (typeof value !== 'object' || value === null) return null
  const item = value as Record<string, unknown>
  if (typeof item.id !== 'string' || item.id === '') return null
  if (typeof item.title !== 'string') return null
  return {
    id: item.id,
    title: item.title,
    type: typeof item.type === 'string' ? item.type : '',
    price: toFiniteNumber(item.price),
    location:
      typeof item.location === 'string' && item.location.trim() !== ''
        ? item.location
        : null,
    creator: toPublicCreator(item.creator),
  }
}

// Converts an API response into safe values. Returns null when the response
// does not have the expected shape (the UI shows the error state).
export function toListData(value: unknown): PetAdListData | null {
  if (typeof value !== 'object' || value === null) return null
  const data = value as Record<string, unknown>
  if (!Array.isArray(data.items)) return null

  const totalCount = toInteger(data.totalCount, 0)
  const page = toInteger(data.page, 1)
  const pageSize = toInteger(data.pageSize, 1)
  if (totalCount === null || page === null || pageSize === null) return null

  const items: PetAdSummary[] = []
  for (const raw of data.items) {
    const item = toItem(raw)
    if (item === null) return null
    items.push(item)
  }

  return { items, totalCount, page, pageSize }
}
