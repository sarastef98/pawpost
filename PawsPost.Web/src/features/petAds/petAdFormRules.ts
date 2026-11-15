import type { components } from '../../api/schema'

type Schemas = components['schemas']
export type PetSpecies = Schemas['PetSpecies']
export type PetAdType = Schemas['PetAdType']
export type CreatePetAdBody = Schemas['CreatePetAdRequest']

// Convenience limits that mirror the server (CreatePetAdRequest.cs). The
// server still decides.
export const LIMITS = {
  titleMax: 100,
  breedMax: 100,
  descriptionMax: 2000,
  locationMax: 100,
  ageMaxMonths: 360,
  priceMax: 1_000_000,
  priceDecimals: 2,
} as const

// Typed from the schema: a Record needs every species, so a new value in the
// backend enum is a compile error here. The order is the order of the list.
const SPECIES_LABELS: Record<PetSpecies, string> = {
  Dog: 'Dog',
  Cat: 'Cat',
  Bird: 'Bird',
  Rabbit: 'Rabbit',
  Other: 'Other',
}

export const SPECIES_OPTIONS: { value: PetSpecies; label: string }[] = (
  Object.keys(SPECIES_LABELS) as PetSpecies[]
).map((value) => ({ value, label: SPECIES_LABELS[value] }))

const TYPE_LABELS: Record<PetAdType, string> = {
  Adoption: 'Adoption',
  Sale: 'Sale',
}

export const TYPE_OPTIONS: { value: PetAdType; label: string }[] = (
  Object.keys(TYPE_LABELS) as PetAdType[]
).map((value) => ({ value, label: TYPE_LABELS[value] }))

export type AgeUnit = 'months' | 'years'

export type FormValues = {
  title: string
  type: '' | PetAdType
  species: '' | PetSpecies
  breed: string
  age: string
  ageUnit: AgeUnit
  description: string
  price: string
  location: string
}

export type FieldName =
  | 'title'
  | 'type'
  | 'species'
  | 'breed'
  | 'age'
  | 'description'
  | 'price'
  | 'location'

export type FieldErrors = Partial<Record<FieldName, string>>

export const EMPTY_VALUES: FormValues = {
  title: '',
  type: '',
  species: '',
  breed: '',
  age: '',
  ageUnit: 'months',
  description: '',
  price: '',
  location: '',
}

const NON_NEGATIVE_DECIMAL = /^\d+(\.\d+)?$/

// Age as whole months, or null when the text is not a usable number.
// Months: the value as is (must be whole). Years: rounded to whole months.
export function toAgeInMonths(age: string, unit: AgeUnit): number | null {
  const text = age.trim()
  if (!NON_NEGATIVE_DECIMAL.test(text)) return null
  const value = Number(text)
  if (!Number.isFinite(value)) return null
  if (unit === 'months') return Number.isInteger(value) ? value : null
  return Math.round(value * 12)
}

function validateAge(age: string, unit: AgeUnit): string | undefined {
  const text = age.trim()
  if (text === '') return 'Please enter the age.'
  if (!NON_NEGATIVE_DECIMAL.test(text)) {
    return 'Age must be a number that is not negative.'
  }
  if (unit === 'months' && !Number.isInteger(Number(text))) {
    return 'Age in months must be a whole number.'
  }
  const months = toAgeInMonths(text, unit)
  if (months === null || months > LIMITS.ageMaxMonths) {
    const years = LIMITS.ageMaxMonths / 12
    return `Age can be at most ${years} years (${LIMITS.ageMaxMonths} months).`
  }
  return undefined
}

function validatePrice(price: string): string | undefined {
  const text = price.trim()
  if (text === '') return 'Please enter the price.'
  if (!NON_NEGATIVE_DECIMAL.test(text)) {
    return 'Price must be a number greater than 0.'
  }
  const decimals = text.split('.')[1]?.length ?? 0
  if (decimals > LIMITS.priceDecimals) {
    return `Price can have at most ${LIMITS.priceDecimals} decimals.`
  }
  const value = Number(text)
  if (!(value > 0)) return 'Price must be greater than 0.'
  if (value > LIMITS.priceMax) {
    return `Price can be at most ${LIMITS.priceMax.toLocaleString('en-US')}.`
  }
  return undefined
}

function tooLong(label: string, max: number): string {
  return `${label} can be at most ${max} characters.`
}

// Client checks, in form order. Convenience only: the server decides.
export function validateForm(values: FormValues): FieldErrors {
  const errors: FieldErrors = {}

  const title = values.title.trim()
  if (title === '') errors.title = 'Please enter a title.'
  else if (title.length > LIMITS.titleMax) {
    errors.title = tooLong('Title', LIMITS.titleMax)
  }

  if (values.type === '') errors.type = 'Please choose a type.'
  if (values.species === '') errors.species = 'Please choose a species.'

  if (values.breed.trim().length > LIMITS.breedMax) {
    errors.breed = tooLong('Breed', LIMITS.breedMax)
  }

  const ageError = validateAge(values.age, values.ageUnit)
  if (ageError) errors.age = ageError

  const description = values.description.trim()
  if (description === '') errors.description = 'Please enter a description.'
  else if (description.length > LIMITS.descriptionMax) {
    errors.description = tooLong('Description', LIMITS.descriptionMax)
  }

  if (values.type === 'Sale') {
    const priceError = validatePrice(values.price)
    if (priceError) errors.price = priceError
  }

  if (values.location.trim().length > LIMITS.locationMax) {
    errors.location = tooLong('Location', LIMITS.locationMax)
  }

  return errors
}

// Builds the request body from values that passed validateForm. Returns null
// when they did not. Empty optional fields and the price of an Adoption ad
// are left out.
export function buildRequestBody(values: FormValues): CreatePetAdBody | null {
  if (values.type === '' || values.species === '') return null
  const ageInMonths = toAgeInMonths(values.age, values.ageUnit)
  if (ageInMonths === null) return null

  const body: CreatePetAdBody = {
    title: values.title.trim(),
    type: values.type,
    species: values.species,
    ageInMonths,
    description: values.description.trim(),
  }
  const breed = values.breed.trim()
  if (breed) body.breed = breed
  const location = values.location.trim()
  if (location) body.location = location
  if (values.type === 'Sale') {
    const price = Number(values.price.trim())
    if (!Number.isFinite(price)) return null
    body.price = price
  }
  return body
}
