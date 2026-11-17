import { describe, expect, it } from 'vitest'
import {
  parseClosedAdsPage,
  parseMyAdsPage,
  parseProfilePage,
  toCreatorProfile,
} from './creatorProfileData'

function body(overrides: Record<string, unknown> = {}) {
  return {
    id: 'creator-1',
    displayName: 'Anna',
    ads: {
      items: [
        {
          id: 'ad-1',
          title: 'Rex',
          type: 'Adoption',
          price: null,
          location: 'Skopje',
          creator: { id: 'creator-1', displayName: 'Anna' },
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    },
    ...overrides,
  }
}

describe('toCreatorProfile', () => {
  it('converts a valid body', () => {
    const profile = toCreatorProfile(body())

    expect(profile?.id).toBe('creator-1')
    expect(profile?.displayName).toBe('Anna')
    expect(profile?.ads.items.map((ad) => ad.id)).toEqual(['ad-1'])
    expect(profile?.ads.items[0].creator).toEqual({
      id: 'creator-1',
      displayName: 'Anna',
    })
    expect(profile?.ads.totalCount).toBe(1)
  })

  it('accepts a profile without ads', () => {
    const profile = toCreatorProfile(
      body({ ads: { items: [], totalCount: 0, page: 1, pageSize: 20 } }),
    )

    expect(profile?.ads.items).toEqual([])
  })

  it('keeps only the known fields', () => {
    const profile = toCreatorProfile(body({ email: 'anna@example.com' }))

    expect(profile).not.toBeNull()
    expect(Object.keys(profile ?? {}).sort()).toEqual([
      'ads',
      'displayName',
      'id',
    ])
  })

  it('gives null when the id is missing', () => {
    const data = body()
    delete (data as Record<string, unknown>).id

    expect(toCreatorProfile(data)).toBeNull()
  })

  it('gives null when the id is empty', () => {
    expect(toCreatorProfile(body({ id: '' }))).toBeNull()
  })

  it('gives null when the display name is not text', () => {
    expect(toCreatorProfile(body({ displayName: 5 }))).toBeNull()
  })

  it('gives null when ads is missing', () => {
    const data = body()
    delete (data as Record<string, unknown>).ads

    expect(toCreatorProfile(data)).toBeNull()
  })

  it('gives null when ads is invalid', () => {
    expect(toCreatorProfile(body({ ads: { items: 'none' } }))).toBeNull()
  })

  it.each([null, undefined, 'text', 42])(
    'gives null for %j instead of an object',
    (value) => {
      expect(toCreatorProfile(value)).toBeNull()
    },
  )
})

describe('parseProfilePage', () => {
  it.each([
    ['page=1', 1],
    ['page=2', 2],
    ['page=15', 15],
  ])('accepts %s', (search, expected) => {
    expect(parseProfilePage(new URLSearchParams(search))).toBe(expected)
  })

  it.each(['', 'page=0', 'page=-3', 'page=abc', 'page=1.5', 'page='])(
    'falls back to page 1 for "%s"',
    (search) => {
      expect(parseProfilePage(new URLSearchParams(search))).toBe(1)
    },
  )
})

describe('parseMyAdsPage', () => {
  it.each([
    ['myAdsPage=1', 1],
    ['myAdsPage=2', 2],
    ['myAdsPage=15', 15],
  ])('accepts %s', (search, expected) => {
    expect(parseMyAdsPage(new URLSearchParams(search))).toBe(expected)
  })

  it.each([
    '',
    'myAdsPage=0',
    'myAdsPage=-3',
    'myAdsPage=abc',
    'myAdsPage=1.5',
    'myAdsPage=',
  ])('falls back to page 1 for "%s"', (search) => {
    expect(parseMyAdsPage(new URLSearchParams(search))).toBe(1)
  })

  it('ignores the public page key', () => {
    expect(parseMyAdsPage(new URLSearchParams('page=4'))).toBe(1)
  })

  it('reads its own key when both keys are present', () => {
    expect(parseMyAdsPage(new URLSearchParams('page=4&myAdsPage=2'))).toBe(2)
  })
})

describe('parseClosedAdsPage', () => {
  it.each([
    ['closedAdsPage=1', 1],
    ['closedAdsPage=2', 2],
    ['closedAdsPage=15', 15],
  ])('accepts %s', (search, expected) => {
    expect(parseClosedAdsPage(new URLSearchParams(search))).toBe(expected)
  })

  it.each([
    '',
    'closedAdsPage=0',
    'closedAdsPage=-3',
    'closedAdsPage=abc',
    'closedAdsPage=1.5',
    'closedAdsPage=',
    'closedAdsPage=99999999999999999999',
  ])('falls back to page 1 for "%s"', (search) => {
    expect(parseClosedAdsPage(new URLSearchParams(search))).toBe(1)
  })

  it('ignores the public page and My ads page keys', () => {
    expect(parseClosedAdsPage(new URLSearchParams('page=4&myAdsPage=3'))).toBe(
      1,
    )
  })

  it('reads its own key when the other keys are present', () => {
    expect(
      parseClosedAdsPage(
        new URLSearchParams('page=4&myAdsPage=3&closedAdsPage=2'),
      ),
    ).toBe(2)
  })
})
