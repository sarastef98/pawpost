import type { FieldErrors, FieldName } from './petAdFormRules'

export const CREATE_FAILED_MESSAGE =
  'We could not post your ad. Please check your connection and try again.'

export type CreateAdErrors = {
  fields: FieldErrors
  general?: string
}

const FIELD_KEYS: Record<string, FieldName> = {
  title: 'title',
  type: 'type',
  species: 'species',
  breed: 'breed',
  ageinmonths: 'age',
  description: 'description',
  price: 'price',
  location: 'location',
}

function readFieldErrors(error: unknown): Record<string, string[]> {
  if (typeof error !== 'object' || error === null || !('errors' in error)) {
    return {}
  }
  const errors = error.errors
  if (typeof errors !== 'object' || errors === null) return {}
  const result: Record<string, string[]> = {}
  for (const [key, value] of Object.entries(errors)) {
    if (Array.isArray(value)) {
      result[key] = value.filter(
        (item): item is string => typeof item === 'string',
      )
    }
  }
  return result
}

// Model-binding and JSON errors can come as "$.species". Strip the leading
// "$" and "." and ignore case.
function normalizeKey(key: string): string {
  return key.replace(/^\$/, '').replace(/^\./, '').toLowerCase()
}

// Turns a 400 body (ValidationProblemDetails) into field messages. Unknown
// keys go to the general message. Server text is shown as sent. A response
// can name only some fields (for example only species), so the absence of
// other errors does not mean the other fields are fine.
export function mapCreateAdErrors(
  status: number,
  error: unknown,
): CreateAdErrors {
  if (status !== 400) return { fields: {}, general: CREATE_FAILED_MESSAGE }

  const fields: FieldErrors = {}
  const general: string[] = []
  for (const [key, messages] of Object.entries(readFieldErrors(error))) {
    const text = messages.join(' ')
    if (!text) continue
    const field = FIELD_KEYS[normalizeKey(key)]
    if (field) {
      fields[field] = fields[field] ? `${fields[field]} ${text}` : text
    } else {
      general.push(text)
    }
  }

  if (general.length > 0) return { fields, general: general.join(' ') }
  if (Object.keys(fields).length === 0) {
    return { fields, general: CREATE_FAILED_MESSAGE }
  }
  return { fields }
}

// Used when the request itself failed (network down, unreadable answer).
export function networkCreateErrors(): CreateAdErrors {
  return { fields: {}, general: CREATE_FAILED_MESSAGE }
}
