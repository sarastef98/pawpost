import { describe, expect, it } from 'vitest'
import {
  EMAIL_TAKEN_MESSAGE,
  GENERIC_MESSAGE,
  WRONG_CREDENTIALS_MESSAGE,
  mapFormErrors,
  networkFormErrors,
} from './authErrors'

describe('mapFormErrors', () => {
  it('puts the Email and Password messages on the matching fields for a 400', () => {
    const result = mapFormErrors('register', 400, {
      errors: { Email: ['Email is not valid.'], Password: ['Too short.'] },
    })

    expect(result).toEqual({
      email: 'Email is not valid.',
      password: 'Too short.',
    })
  })

  it.each(['email', 'EMAIL', 'eMaIl'])(
    'matches the field key %s to the email field',
    (key) => {
      const result = mapFormErrors('login', 400, {
        errors: { [key]: ['Bad.'] },
      })

      expect(result.email).toBe('Bad.')
      expect(result.general).toBeUndefined()
    },
  )

  it.each(['password', 'PASSWORD', 'PassWord'])(
    'matches the field key %s to the password field',
    (key) => {
      const result = mapFormErrors('login', 400, {
        errors: { [key]: ['Bad.'] },
      })

      expect(result.password).toBe('Bad.')
      expect(result.general).toBeUndefined()
    },
  )

  it('joins several messages for one field', () => {
    const result = mapFormErrors('register', 400, {
      errors: { Password: ['Needs a digit.', 'Needs a symbol.'] },
    })

    expect(result.password).toBe('Needs a digit. Needs a symbol.')
  })

  it('sends messages for an unknown key to the general message', () => {
    const result = mapFormErrors('register', 400, {
      errors: { Other: ['Something odd.'] },
    })

    expect(result).toEqual({ general: 'Something odd.' })
  })

  it('sends messages for an empty key to the general message', () => {
    const result = mapFormErrors('register', 400, {
      errors: { '': ['Empty key message.'] },
    })

    expect(result).toEqual({ general: 'Empty key message.' })
  })

  it('keeps field messages and the general message apart', () => {
    const result = mapFormErrors('register', 400, {
      errors: { Email: ['Bad email.'], '': ['General one.'] },
    })

    expect(result).toEqual({ email: 'Bad email.', general: 'General one.' })
  })

  it.each([
    ['no body', undefined],
    ['a null body', null],
    ['a body without errors', { title: 'Bad' }],
    ['errors that are not an object', { errors: 'nope' }],
    ['an empty errors map', { errors: {} }],
    ['only empty message lists', { errors: { Email: [] } }],
    ['only non-text messages', { errors: { Email: [1, null] } }],
  ])('gives the generic message for a 400 with %s', (_name, body) => {
    expect(mapFormErrors('login', 400, body)).toEqual({
      general: GENERIC_MESSAGE,
    })
  })

  it('gives the wrong credentials message for a login 401', () => {
    expect(mapFormErrors('login', 401, { title: 'Unauthorized' })).toEqual({
      general: WRONG_CREDENTIALS_MESSAGE,
    })
  })

  it('gives the email taken message for a register 409', () => {
    expect(mapFormErrors('register', 409, { title: 'Conflict' })).toEqual({
      general: EMAIL_TAKEN_MESSAGE,
      emailTaken: true,
    })
  })

  it('gives the generic message for a register 401', () => {
    expect(mapFormErrors('register', 401, {})).toEqual({
      general: GENERIC_MESSAGE,
    })
  })

  it('gives the generic message for a login 409', () => {
    expect(mapFormErrors('login', 409, {})).toEqual({
      general: GENERIC_MESSAGE,
    })
  })

  it('gives the generic message for an unexpected status and shows no status text', () => {
    const result = mapFormErrors('login', 500, {
      title: 'Internal Server Error',
      detail: 'stack trace',
    })

    expect(result).toEqual({ general: GENERIC_MESSAGE })
    expect(result.general).not.toMatch(/500|stack|Internal/)
  })
})

describe('networkFormErrors', () => {
  it('gives the generic friendly message', () => {
    expect(networkFormErrors()).toEqual({ general: GENERIC_MESSAGE })
  })
})

describe('mapFormErrors public name', () => {
  const INVALID =
    'Please use 3 to 30 characters: letters, digits, single spaces, hyphens or underscores.'
  const NAME_TAKEN = 'This name is already taken. Please choose another.'
  const EMAIL_TAKEN =
    'This email is already registered. Try logging in instead.'

  it.each(['DisplayName', 'displayName', 'DISPLAYNAME', 'displayname'])(
    'puts the %s key on the displayName field with the fixed friendly text',
    (key) => {
      const errors = mapFormErrors('register', 400, {
        errors: { [key]: ['The DisplayName field must be between 3 and 30.'] },
      })

      expect(errors).toEqual({ displayName: INVALID })
    },
  )

  it('does not show the server text or the field name for the name', () => {
    const errors = mapFormErrors('register', 400, {
      errors: { DisplayName: ['The DisplayName field is invalid.'] },
    })

    expect(errors.displayName).not.toMatch(/DisplayName field/)
    expect(errors.general).toBeUndefined()
  })

  it('keeps the email and password messages as sent next to the name message', () => {
    const errors = mapFormErrors('register', 400, {
      errors: {
        Email: ['Email is not valid.'],
        Password: ['Too short.'],
        DisplayName: ['Bad name.'],
      },
    })

    expect(errors).toEqual({
      email: 'Email is not valid.',
      password: 'Too short.',
      displayName: INVALID,
    })
  })

  it('gives the name taken message for a 409 with the exact title', () => {
    const errors = mapFormErrors('register', 409, {
      title: 'Display name already taken',
    })

    expect(errors).toEqual({ displayName: NAME_TAKEN })
  })

  it.each([
    [{ title: 'Email already registered' }],
    [{ title: 'display name already taken' }],
    [{ title: 'Conflict' }],
    [{}],
    [undefined],
  ])('keeps the email taken message for a 409 with body %j', (body) => {
    const errors = mapFormErrors('register', 409, body)

    expect(errors).toEqual({ general: EMAIL_TAKEN, emailTaken: true })
  })

  it('does not use the name taken title for a login 409', () => {
    const errors = mapFormErrors('login', 409, {
      title: 'Display name already taken',
    })

    expect(errors.displayName).toBeUndefined()
    expect(errors.emailTaken).toBeUndefined()
  })
})
