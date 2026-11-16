import type { components } from '../../api/schema'
import { toPetAdDetails } from '../petAds/petAdDetailsData'
import type { PetAdDetails } from '../petAds/petAdDetailsData'

type Response = components['schemas']['AssistantSearchResponse']

export type AssistantResult = Pick<Response, 'answer'> & {
  ads: PetAdDetails[]
}

// Converts the 200 body into safe values. Returns null when the answer is not
// text, the ads are not a list, or any ad is unreadable (the UI then shows the
// error state).
export function toAssistantResult(value: unknown): AssistantResult | null {
  if (typeof value !== 'object' || value === null) return null
  const data = value as Record<string, unknown>
  if (typeof data.answer !== 'string') return null
  if (!Array.isArray(data.ads)) return null

  const ads: PetAdDetails[] = []
  for (const item of data.ads) {
    const ad = toPetAdDetails(item)
    if (ad === null) return null
    ads.push(ad)
  }
  return { answer: data.answer, ads }
}
