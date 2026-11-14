import { describe, expect, it } from 'vitest'
import {
  buildSearchParams,
  parseListQuery,
  toListData,
  totalPages,
} from './petAdListQuery'

function parse(search: string) {
  return parseListQuery(new URLSearchParams(search))
}

describe('parseListQuery', () => {
  it('gives the defaults for an empty query string', () => {
    expect(parse('')).toEqual({ type: '', species: '', page: 1 })
  })

  it('reads type, species and page', () => {
    expect(parse('type=Sale&species=Dog&page=3')).toEqual({
      type: 'Sale',
      species: 'Dog',
      page: 3,
    })
  })

  it('trims the species', () => {
    expect(parse('species=%20Dog%20').species).toBe('Dog')
  })

  it.each(['5', 'Cat', 'sale', 'ADOPTION', ''])(
    'ignores the invalid type "%s"',
    (value) => {
      expect(parse(`type=${value}`).type).toBe('')
    },
  )

  it.each(['0', '-2', 'abc', '1.5', '01', '2147483648', ''])(
    'ignores the invalid page "%s"',
    (value) => {
      expect(parse(`page=${value}`).page).toBe(1)
    },
  )

  it('accepts the largest allowed page', () => {
    expect(parse('page=2147483647').page).toBe(2147483647)
  })

  it('accepts a species of 50 characters and ignores one of 51', () => {
    expect(parse(`species=${'a'.repeat(50)}`).species).toBe('a'.repeat(50))
    expect(parse(`species=${'a'.repeat(51)}`).species).toBe('')
  })
})

describe('buildSearchParams', () => {
  it('leaves out empty filters and page 1', () => {
    const params = buildSearchParams({ type: '', species: '  ', page: 1 })

    expect(params.toString()).toBe('')
  })

  it('writes type, species and page', () => {
    const params = buildSearchParams({
      type: 'Sale',
      species: ' Dog ',
      page: 2,
    })

    expect(params.get('type')).toBe('Sale')
    expect(params.get('species')).toBe('Dog')
    expect(params.get('page')).toBe('2')
  })
})

describe('totalPages', () => {
  it('rounds up', () => {
    expect(totalPages(41, 20)).toBe(3)
    expect(totalPages(40, 20)).toBe(2)
  })

  it('is 0 when there are no ads', () => {
    expect(totalPages(0, 20)).toBe(0)
  })
})

describe('toListData', () => {
  const item = {
    id: 'a1',
    title: 'Rex',
    type: 'Sale',
    price: '1200',
    location: 'Skopje',
  }

  it('converts numbers sent as strings', () => {
    const data = toListData({
      items: [item],
      totalCount: '57',
      page: '2',
      pageSize: '20',
    })

    expect(data).toEqual({
      items: [{ ...item, price: 1200 }],
      totalCount: 57,
      page: 2,
      pageSize: 20,
    })
  })

  it('turns a missing location into null', () => {
    const data = toListData({
      items: [{ ...item, location: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    })

    expect(data?.items[0].location).toBeNull()
  })

  it('turns an invalid price into null', () => {
    const data = toListData({
      items: [{ ...item, price: 'abc' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    })

    expect(data?.items[0].price).toBeNull()
  })

  it.each([
    ['totalCount is not a number', { totalCount: 'many' }],
    ['totalCount is negative', { totalCount: -1 }],
    ['totalCount has decimals', { totalCount: 1.5 }],
    ['page is 0', { page: 0 }],
    ['pageSize is 0', { pageSize: 0 }],
    ['items is missing', { items: undefined }],
    ['an item has no id', { items: [{ title: 'x' }] }],
  ])('gives null when %s', (_name, override) => {
    const base = { items: [], totalCount: 0, page: 1, pageSize: 20 }

    expect(toListData({ ...base, ...override })).toBeNull()
  })

  it('gives null for something that is not an object', () => {
    expect(toListData(null)).toBeNull()
    expect(toListData('x')).toBeNull()
  })
})
