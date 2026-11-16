import { parseListQuery, toListData } from '../petAds/petAdListQuery'
import type { PetAdListData } from '../petAds/petAdListQuery'

// What the profile page shows: the public name and the creator's ads.
export type CreatorProfile = {
  id: string
  displayName: string
  ads: PetAdListData
}

// Converts an API response into safe values. Returns null when the response
// does not have the expected shape (the UI shows the error state). Only the
// fields listed here are kept, so extra fields never reach the screen.
export function toCreatorProfile(value: unknown): CreatorProfile | null {
  if (typeof value !== 'object' || value === null) return null
  const data = value as Record<string, unknown>
  if (typeof data.id !== 'string' || data.id === '') return null
  if (typeof data.displayName !== 'string') return null
  const ads = toListData(data.ads)
  if (ads === null) return null
  return { id: data.id, displayName: data.displayName, ads }
}

// Reads the page from the URL, with the same rules as the home page list.
// Anything that is not a positive whole number gives page 1.
export function parseProfilePage(params: URLSearchParams): number {
  return parseListQuery(params).page
}
