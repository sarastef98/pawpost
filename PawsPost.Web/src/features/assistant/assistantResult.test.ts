import { describe, expect, it } from 'vitest'
import { toAssistantResult } from './assistantResult'

function ad(overrides: Record<string, unknown> = {}) {
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

describe('toAssistantResult', () => {
  it('converts a valid body', () => {
    const result = toAssistantResult({
      answer: 'Here is a dog.',
      ads: [ad(), ad({ id: 'ad-2', title: 'Misty' })],
    })

    expect(result?.answer).toBe('Here is a dog.')
    expect(result?.ads.map((a) => a.id)).toEqual(['ad-1', 'ad-2'])
  })

  it('accepts an empty ads list', () => {
    expect(toAssistantResult({ answer: 'Sorry.', ads: [] })).toEqual({
      answer: 'Sorry.',
      ads: [],
    })
  })

  it('returns null when the answer is missing', () => {
    expect(toAssistantResult({ ads: [] })).toBeNull()
  })

  it('returns null when the answer is not text', () => {
    expect(toAssistantResult({ answer: 42, ads: [] })).toBeNull()
  })

  it('returns null when ads is missing', () => {
    expect(toAssistantResult({ answer: 'Hi' })).toBeNull()
  })

  it('returns null when ads is not an array', () => {
    expect(toAssistantResult({ answer: 'Hi', ads: 'none' })).toBeNull()
  })

  it('returns null when one ad is unreadable', () => {
    expect(
      toAssistantResult({ answer: 'Hi', ads: [ad(), { id: 'ad-2' }] }),
    ).toBeNull()
  })

  it.each([null, undefined, 'text', 42])(
    'returns null for %j instead of an object',
    (value) => {
      expect(toAssistantResult(value)).toBeNull()
    },
  )

  it('accepts numeric strings for price and age', () => {
    const result = toAssistantResult({
      answer: 'Hi',
      ads: [ad({ price: '150.50', ageInMonths: '14' })],
    })

    expect(result?.ads[0].price).toBe(150.5)
    expect(result?.ads[0].ageInMonths).toBe(14)
  })
})

describe('toAssistantResult creator', () => {
  it('keeps the creator of each ad', () => {
    const result = toAssistantResult({
      answer: 'Hi',
      ads: [
        ad({ creator: { id: 'c1', displayName: 'Anna' } }),
        ad({ id: 'ad-2', creator: { id: 'c2', displayName: 'Boris' } }),
      ],
    })

    expect(result?.ads.map((a) => a.creator)).toEqual([
      { id: 'c1', displayName: 'Anna' },
      { id: 'c2', displayName: 'Boris' },
    ])
  })

  it('keeps an ad with an unreadable creator and gives it a null creator', () => {
    const result = toAssistantResult({
      answer: 'Hi',
      ads: [ad({ creator: { id: '' } }), ad({ id: 'ad-2' })],
    })

    expect(result?.ads.map((a) => a.id)).toEqual(['ad-1', 'ad-2'])
    expect(result?.ads.map((a) => a.creator)).toEqual([null, null])
  })
})
