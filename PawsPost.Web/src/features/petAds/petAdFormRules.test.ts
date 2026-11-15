import { describe, expect, it } from 'vitest'
import {
  EMPTY_VALUES,
  SPECIES_OPTIONS,
  buildRequestBody,
  toAgeInMonths,
  validateForm,
} from './petAdFormRules'
import type { FormValues } from './petAdFormRules'

function valid(overrides: Partial<FormValues> = {}): FormValues {
  return {
    ...EMPTY_VALUES,
    title: 'Friendly Rex',
    type: 'Adoption',
    species: 'Dog',
    age: '8',
    description: 'A calm dog.',
    ...overrides,
  }
}

describe('toAgeInMonths', () => {
  it.each([
    ['8', 'months', 8],
    ['0', 'months', 0],
    ['360', 'months', 360],
    ['361', 'months', 361],
    ['2', 'years', 24],
    ['1.5', 'years', 18],
    ['0.04', 'years', 0],
    ['0.05', 'years', 1],
    ['30', 'years', 360],
    ['30.1', 'years', 361],
  ] as const)('converts %s %s to %s months', (age, unit, expected) => {
    expect(toAgeInMonths(age, unit)).toBe(expected)
  })

  it.each([
    ['', 'months'],
    ['abc', 'years'],
    ['-1', 'months'],
    ['1e3', 'months'],
    ['1.5', 'months'],
  ] as const)('gives null for "%s" in %s', (age, unit) => {
    expect(toAgeInMonths(age, unit)).toBeNull()
  })
})

describe('SPECIES_OPTIONS', () => {
  it('lists exactly the five species in order with the exact names as values', () => {
    expect(SPECIES_OPTIONS.map((o) => o.value)).toEqual([
      'Dog',
      'Cat',
      'Bird',
      'Rabbit',
      'Other',
    ])
  })
})

describe('validateForm', () => {
  it('accepts a valid adoption ad', () => {
    expect(validateForm(valid())).toEqual({})
  })

  it('requires title, type, species, age and description', () => {
    const errors = validateForm(EMPTY_VALUES)

    expect(Object.keys(errors)).toEqual([
      'title',
      'type',
      'species',
      'age',
      'description',
    ])
  })

  it('rejects text that is only spaces as missing', () => {
    expect(validateForm(valid({ title: '   ' })).title).toBeDefined()
  })

  it('checks trimmed maximum lengths', () => {
    expect(
      validateForm(valid({ title: ` ${'a'.repeat(100)} ` })).title,
    ).toBeUndefined()
    const errors = validateForm(
      valid({
        title: 'a'.repeat(101),
        breed: 'a'.repeat(101),
        description: 'a'.repeat(2001),
        location: 'a'.repeat(101),
      }),
    )
    expect(errors.title).toBeDefined()
    expect(errors.breed).toBeDefined()
    expect(errors.description).toBeDefined()
    expect(errors.location).toBeDefined()
  })

  it.each([
    ['360', 'months', false],
    ['361', 'months', true],
    ['30', 'years', false],
    ['30.1', 'years', true],
    ['-1', 'months', true],
    ['abc', 'years', true],
    ['1.5', 'months', true],
    ['0', 'months', false],
  ] as const)('age %s %s has error: %s', (age, ageUnit, hasError) => {
    expect(validateForm(valid({ age, ageUnit })).age !== undefined).toBe(
      hasError,
    )
  })

  it('names the unit in the age message', () => {
    expect(validateForm(valid({ age: '31', ageUnit: 'years' })).age).toBe(
      'Age can be at most 30 years (360 months).',
    )
  })

  it('requires a price for Sale only', () => {
    expect(validateForm(valid({ type: 'Sale' })).price).toBeDefined()
    expect(validateForm(valid({ type: 'Adoption', price: '' })).price).toBe(
      undefined,
    )
  })

  it.each([
    ['0', true],
    ['-5', true],
    ['abc', true],
    ['10.999', true],
    ['1000000.01', true],
    ['1000000', false],
    ['0.01', false],
    ['150.5', false],
  ] as const)('price %s has error: %s', (price, hasError) => {
    expect(
      validateForm(valid({ type: 'Sale', price })).price !== undefined,
    ).toBe(hasError)
  })

  it('ignores an invalid price when the type is Adoption', () => {
    expect(validateForm(valid({ price: 'abc' }))).toEqual({})
  })
})

describe('buildRequestBody', () => {
  it('trims text and omits empty optional fields', () => {
    const body = buildRequestBody(
      valid({ title: '  Rex ', breed: '  ', location: '' }),
    )

    expect(body).toEqual({
      title: 'Rex',
      type: 'Adoption',
      species: 'Dog',
      ageInMonths: 8,
      description: 'A calm dog.',
    })
  })

  it('includes trimmed breed and location', () => {
    const body = buildRequestBody(
      valid({ breed: ' Lab ', location: ' Skopje' }),
    )

    expect(body).toMatchObject({ breed: 'Lab', location: 'Skopje' })
  })

  it('sends the price as a number for Sale', () => {
    const body = buildRequestBody(valid({ type: 'Sale', price: '150.50' }))

    expect(body?.price).toBe(150.5)
  })

  it('omits the price for Adoption even when one was typed', () => {
    const body = buildRequestBody(valid({ price: '150' }))

    expect(body).not.toHaveProperty('price')
  })

  it('converts years to months', () => {
    const body = buildRequestBody(valid({ age: '1.5', ageUnit: 'years' }))

    expect(body?.ageInMonths).toBe(18)
  })

  it('returns null when the values are not valid', () => {
    expect(buildRequestBody(EMPTY_VALUES)).toBeNull()
  })
})
