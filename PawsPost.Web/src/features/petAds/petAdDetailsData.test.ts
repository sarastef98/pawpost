import { describe, expect, it } from 'vitest'
import { formatAge, toPetAdDetails } from './petAdDetailsData'

function body(overrides: Record<string, unknown> = {}) {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Sale',
    species: 'Dog',
    breed: 'Labrador',
    ageInMonths: 27,
    description: 'A calm dog.',
    price: 150.5,
    location: 'Skopje',
    status: 'Active',
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

describe('toPetAdDetails', () => {
  it('converts a valid body', () => {
    expect(toPetAdDetails(body())).toEqual({
      id: 'ad-1',
      title: 'Friendly Rex',
      type: 'Sale',
      species: 'Dog',
      breed: 'Labrador',
      ageInMonths: 27,
      description: 'A calm dog.',
      price: 150.5,
      location: 'Skopje',
      creator: null,
    })
  })

  it('converts numeric strings for price and age', () => {
    const ad = toPetAdDetails(body({ price: '150.50', ageInMonths: '14' }))

    expect(ad?.price).toBe(150.5)
    expect(ad?.ageInMonths).toBe(14)
  })

  it.each(['id', 'title', 'species', 'description'])(
    'returns null when %s is missing',
    (field) => {
      const data = body()
      delete (data as Record<string, unknown>)[field]

      expect(toPetAdDetails(data)).toBeNull()
    },
  )

  it.each([null, undefined, 'text', 42])(
    'returns null for %j instead of an object',
    (value) => {
      expect(toPetAdDetails(value)).toBeNull()
    },
  )

  it('gives a null price for an invalid or missing price', () => {
    expect(toPetAdDetails(body({ price: 'free' }))?.price).toBeNull()
    expect(toPetAdDetails(body({ price: null }))?.price).toBeNull()
  })

  it('leaves a negative price to formatPrice, which shows nothing', () => {
    expect(toPetAdDetails(body({ price: -5 }))?.price).toBe(-5)
  })

  it('turns an empty or blank breed into null', () => {
    expect(toPetAdDetails(body({ breed: '' }))?.breed).toBeNull()
    expect(toPetAdDetails(body({ breed: '   ' }))?.breed).toBeNull()
    expect(toPetAdDetails(body({ breed: null }))?.breed).toBeNull()
  })

  it('turns an empty or blank location into null', () => {
    expect(toPetAdDetails(body({ location: '' }))?.location).toBeNull()
    expect(toPetAdDetails(body({ location: '  ' }))?.location).toBeNull()
  })

  it('turns a negative, fractional or invalid age into null', () => {
    expect(toPetAdDetails(body({ ageInMonths: -1 }))?.ageInMonths).toBeNull()
    expect(toPetAdDetails(body({ ageInMonths: 1.5 }))?.ageInMonths).toBeNull()
    expect(toPetAdDetails(body({ ageInMonths: 'old' }))?.ageInMonths).toBeNull()
  })
})

describe('formatAge', () => {
  it.each([
    [0, '0 months'],
    [1, '1 month'],
    [11, '11 months'],
    [12, '1 year'],
    [13, '1 year 1 month'],
    [24, '2 years'],
    [27, '2 years 3 months'],
    ['27', '2 years 3 months'],
  ])('formats %j months as "%s"', (months, text) => {
    expect(formatAge(months)).toBe(text)
  })

  it.each([-1, 1.5, 'abc', null, undefined, NaN])(
    'returns null for invalid input %j',
    (value) => {
      expect(formatAge(value)).toBeNull()
    },
  )
})
