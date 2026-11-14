import { describe, expect, it } from 'vitest'
import {
  PRICE_CURRENCY,
  PRICE_LOCALE,
  formatPrice,
  toFiniteNumber,
} from './formatPrice'

describe('formatPrice', () => {
  it('uses the fixed euro currency and locale', () => {
    expect(PRICE_CURRENCY).toBe('EUR')
    expect(PRICE_LOCALE).toBe('en-IE')
  })

  it('puts the euro symbol before the amount with two decimals', () => {
    expect(formatPrice('Sale', 150.5)).toBe('€150.50')
    expect(formatPrice('Sale', 150)).toBe('€150.00')
  })

  it('adds thousands separators', () => {
    expect(formatPrice('Sale', 1200)).toBe('€1,200.00')
  })

  it('accepts a price sent as a string', () => {
    expect(formatPrice('Sale', '1200')).toBe('€1,200.00')
  })

  it('shows a zero price', () => {
    expect(formatPrice('Sale', 0)).toBe('€0.00')
  })

  it('gives nothing for an Adoption ad, even with a price', () => {
    expect(formatPrice('Adoption', 100)).toBeNull()
  })

  it('gives nothing for an unexpected type', () => {
    expect(formatPrice('Swap', 100)).toBeNull()
  })

  it('gives nothing for a null or missing price', () => {
    expect(formatPrice('Sale', null)).toBeNull()
    expect(formatPrice('Sale', undefined)).toBeNull()
  })

  it('gives nothing for an invalid or negative price', () => {
    expect(formatPrice('Sale', 'abc')).toBeNull()
    expect(formatPrice('Sale', '')).toBeNull()
    expect(formatPrice('Sale', NaN)).toBeNull()
    expect(formatPrice('Sale', Infinity)).toBeNull()
    expect(formatPrice('Sale', -5)).toBeNull()
  })

  it.each(['0x10', '1e3', ' 12 ', '', 'abc'])(
    'gives nothing for the price string %j that is not a plain decimal',
    (price) => {
      expect(formatPrice('Sale', price)).toBeNull()
    },
  )

  it('accepts a plain decimal price string with cents', () => {
    expect(formatPrice('Sale', '150.50')).toBe('€150.50')
  })
})

describe('toFiniteNumber', () => {
  it('converts numbers and numeric strings', () => {
    expect(toFiniteNumber(3)).toBe(3)
    expect(toFiniteNumber('3.5')).toBe(3.5)
  })

  it('gives null for everything else', () => {
    expect(toFiniteNumber(null)).toBeNull()
    expect(toFiniteNumber('  ')).toBeNull()
    expect(toFiniteNumber('x')).toBeNull()
    expect(toFiniteNumber({})).toBeNull()
  })
})
