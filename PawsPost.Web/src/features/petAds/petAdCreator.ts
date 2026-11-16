import type { components } from '../../api/schema'

// The only creator data the screens use: the public name, and the id for the
// profile link. No contact details are ever part of it.
export type PublicCreator = Pick<
  components['schemas']['PetAdCreatorResponse'],
  'id' | 'displayName'
>

// Reads the creator of an ad. A missing or unreadable creator gives null, so
// the ad is still shown, just without the creator line.
export function toPublicCreator(value: unknown): PublicCreator | null {
  if (typeof value !== 'object' || value === null) return null
  const creator = value as Record<string, unknown>
  if (typeof creator.id !== 'string' || creator.id === '') return null
  if (typeof creator.displayName !== 'string') return null
  return { id: creator.id, displayName: creator.displayName }
}
