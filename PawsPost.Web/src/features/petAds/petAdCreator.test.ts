import { describe, expect, it } from 'vitest'
import { toPublicCreator } from './petAdCreator'

describe('toPublicCreator', () => {
  it('reads a valid creator', () => {
    expect(toPublicCreator({ id: 'c1', displayName: 'Anna' })).toEqual({
      id: 'c1',
      displayName: 'Anna',
    })
  })

  it('keeps only the id and the display name', () => {
    const creator = toPublicCreator({
      id: 'c1',
      displayName: 'Anna',
      email: 'anna@example.com',
    })

    expect(creator).toEqual({ id: 'c1', displayName: 'Anna' })
  })

  it.each([undefined, null, 'Anna', 42, true])(
    'gives null for %j instead of an object',
    (value) => {
      expect(toPublicCreator(value)).toBeNull()
    },
  )

  it('gives null when the id is missing', () => {
    expect(toPublicCreator({ displayName: 'Anna' })).toBeNull()
  })

  it('gives null when the id is empty', () => {
    expect(toPublicCreator({ id: '', displayName: 'Anna' })).toBeNull()
  })

  it('gives null when the id is not text', () => {
    expect(toPublicCreator({ id: 5, displayName: 'Anna' })).toBeNull()
  })

  it('gives null when the display name is missing', () => {
    expect(toPublicCreator({ id: 'c1' })).toBeNull()
  })

  it('gives null when the display name is not text', () => {
    expect(toPublicCreator({ id: 'c1', displayName: 7 })).toBeNull()
  })
})
