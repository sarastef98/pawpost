// Helpers that build the results the typed API client returns, so tests can
// answer at the client boundary without repeating the shape.

export function okResult(data: unknown, status = 200) {
  return { data, error: undefined, response: { status, ok: true } }
}

export function noContentResult() {
  return {
    data: undefined,
    error: undefined,
    response: { status: 204, ok: true },
  }
}

export function failResult(status: number, error: unknown = {}) {
  return { data: undefined, error, response: { status, ok: false } }
}

export function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => {
    resolve = r
  })
  return { promise, resolve }
}

export const testUser = { id: 'user-1', email: 'sara@example.com' }

// A synthetic pet ad as the API sends it (PetAdDetailsResponse).
export function sampleAd(overrides: Record<string, unknown> = {}) {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Adoption',
    species: 'Dog',
    breed: 'Labrador',
    ageInMonths: 27,
    description: 'A calm dog.',
    price: null,
    location: 'Skopje',
    status: 'Active',
    createdAt: '2026-11-01T10:00:00Z',
    creator: { id: 'creator-1', displayName: 'Pet lover 0001' },
    ...overrides,
  }
}

// A synthetic list item as the API sends it (PetAdListItemResponse).
export function sampleListItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Adoption',
    price: null,
    location: 'Skopje',
    createdAt: '2026-11-01T10:00:00Z',
    creator: { id: 'creator-1', displayName: 'Pet lover 0001' },
    ...overrides,
  }
}

// A 200 body of the public creator profile (CreatorProfileResponse).
export function creatorProfileBody(
  displayName: string,
  items: unknown[] = [],
  totalCount = items.length,
  page = 1,
  pageSize = 20,
) {
  return {
    id: 'creator-1',
    displayName,
    ads: { items, totalCount, page, pageSize },
  }
}

// A 200 body of the assistant search (AssistantSearchResponse).
export function assistantBody(answer: string, ads: unknown[] = []) {
  return { answer, ads }
}
