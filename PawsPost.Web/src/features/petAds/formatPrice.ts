// The API has no currency field. The euro is one fixed choice for every ad,
// and the locale is fixed too, so every visitor sees the same text.
export const PRICE_CURRENCY = 'EUR'
export const PRICE_LOCALE = 'en-IE'

const priceFormat = new Intl.NumberFormat(PRICE_LOCALE, {
  style: 'currency',
  currency: PRICE_CURRENCY,
})

// A plain decimal string such as "12", "-3" or "150.50". No spaces, hex or
// exponent, which Number() would otherwise accept.
const PLAIN_DECIMAL = /^-?\d+(\.\d+)?$/

// Turns a value that may be a number or a plain decimal string into a finite
// number, or null when it is missing or not a number.
export function toFiniteNumber(value: unknown): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null
  }
  if (typeof value === 'string' && PLAIN_DECIMAL.test(value)) {
    const converted = Number(value)
    return Number.isFinite(converted) ? converted : null
  }
  return null
}

// Price text for a card, for example "€150.50". Only Sale ads show a price.
// A missing, negative or invalid price gives null (show nothing).
export function formatPrice(type: unknown, price: unknown): string | null {
  if (type !== 'Sale') return null
  const amount = toFiniteNumber(price)
  if (amount === null || amount < 0) return null
  return priceFormat.format(amount)
}
