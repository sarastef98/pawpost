import { describe, expect, it } from 'vitest'
import {
  CREATE_FAILED_MESSAGE,
  mapCreateAdErrors,
  networkCreateErrors,
} from './petAdFormErrors'

describe('mapCreateAdErrors', () => {
  it('maps keys to fields ignoring case', () => {
    const result = mapCreateAdErrors(400, {
      errors: { Title: ['Title is required.'], DESCRIPTION: ['Too long.'] },
    })

    expect(result).toEqual({
      fields: { title: 'Title is required.', description: 'Too long.' },
    })
  })

  it('strips a leading $. from the key', () => {
    const result = mapCreateAdErrors(400, {
      errors: { '$.species': ['Unknown species.'], $type: ['Bad type.'] },
    })

    expect(result.fields).toEqual({
      species: 'Unknown species.',
      type: 'Bad type.',
    })
  })

  it('maps ageInMonths to the age field', () => {
    const result = mapCreateAdErrors(400, {
      errors: { AgeInMonths: ['Out of range.'] },
    })

    expect(result.fields.age).toBe('Out of range.')
  })

  it('joins several messages for one field', () => {
    const result = mapCreateAdErrors(400, {
      errors: { Price: ['First.', 'Second.'] },
    })

    expect(result.fields.price).toBe('First. Second.')
  })

  it('joins messages from keys that differ only in case', () => {
    const result = mapCreateAdErrors(400, {
      errors: { price: ['One.'], Price: ['Two.'] },
    })

    expect(result.fields.price).toBe('One. Two.')
  })

  it('reports only species when the server sends only species', () => {
    const result = mapCreateAdErrors(400, {
      errors: { species: ['Unknown species.'] },
    })

    expect(result).toEqual({ fields: { species: 'Unknown species.' } })
  })

  it('puts unknown keys in the general message', () => {
    const result = mapCreateAdErrors(400, {
      errors: { Title: ['Bad.'], Other: ['Strange.'], '': ['Empty key.'] },
    })

    expect(result.fields.title).toBe('Bad.')
    expect(result.general).toBe('Strange. Empty key.')
  })

  it.each([
    ['no errors object', {}],
    ['null body', null],
    ['errors is not an object', { errors: 'x' }],
    ['only empty messages', { errors: { Title: [] } }],
  ])('falls back to the friendly message for %s', (_name, body) => {
    expect(mapCreateAdErrors(400, body)).toEqual({
      fields: {},
      general: CREATE_FAILED_MESSAGE,
    })
  })

  it.each([401, 403, 500])('gives the general message for status %s', (s) => {
    expect(mapCreateAdErrors(s, { errors: { Title: ['x'] } })).toEqual({
      fields: {},
      general: CREATE_FAILED_MESSAGE,
    })
  })
})

describe('networkCreateErrors', () => {
  it('gives the general message', () => {
    expect(networkCreateErrors()).toEqual({
      fields: {},
      general: CREATE_FAILED_MESSAGE,
    })
  })
})
