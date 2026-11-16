export const WRONG_CREDENTIALS_MESSAGE =
  'Wrong email or password. Please try again.'
export const EMAIL_TAKEN_MESSAGE =
  'This email is already registered. Try logging in instead.'
export const DISPLAY_NAME_TAKEN_MESSAGE =
  'This name is already taken. Please choose another.'
export const DISPLAY_NAME_INVALID_MESSAGE =
  'Please use 3 to 30 characters: letters, digits, single spaces, hyphens or underscores.'
// Title of the 409 the API sends for a taken public name. It is the only
// way to tell it apart from the taken-email 409.
const DISPLAY_NAME_TAKEN_TITLE = 'Display name already taken'
export const GENERIC_MESSAGE =
  'Something went wrong. Please check your connection and try again.'
export const LOGOUT_FAILED_MESSAGE =
  'We could not log you out. Please try again.'

export type FormErrors = {
  email?: string
  password?: string
  displayName?: string
  general?: string
  // True when the general message is the "email already registered" one,
  // so the page can add a link to the login page.
  emailTaken?: boolean
}

type FormKind = 'login' | 'register'

function readFieldErrors(error: unknown): Record<string, string[]> {
  if (typeof error !== 'object' || error === null || !('errors' in error)) {
    return {}
  }
  const errors = error.errors
  if (typeof errors !== 'object' || errors === null) {
    return {}
  }
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

function readTitle(error: unknown): string | null {
  if (typeof error !== 'object' || error === null || !('title' in error)) {
    return null
  }
  return typeof error.title === 'string' ? error.title : null
}

// Turns a 400 body (ValidationProblemDetails) into field messages. Keys are
// matched to the form fields ignoring upper/lower case. Messages for other or
// empty keys go to the general message. Text is shown as the server sent it.
function fromValidationError(error: unknown): FormErrors {
  const result: FormErrors = {}
  const general: string[] = []
  for (const [key, messages] of Object.entries(readFieldErrors(error))) {
    const text = messages.join(' ')
    if (!text) {
      continue
    }
    const name = key.toLowerCase()
    if (name === 'email') {
      result.email = result.email ? `${result.email} ${text}` : text
    } else if (name === 'password') {
      result.password = result.password ? `${result.password} ${text}` : text
    } else if (name === 'displayname') {
      // The server text names the field in technical words, so one fixed
      // friendly sentence is shown instead.
      result.displayName = DISPLAY_NAME_INVALID_MESSAGE
    } else {
      general.push(text)
    }
  }
  if (general.length > 0) {
    result.general = general.join(' ')
  } else if (!result.email && !result.password && !result.displayName) {
    result.general = GENERIC_MESSAGE
  }
  return result
}

// Maps a finished request (HTTP status plus parsed error body) to what the
// form should show. Status codes and server titles are never shown.
export function mapFormErrors(
  kind: FormKind,
  status: number,
  error: unknown,
): FormErrors {
  if (status === 400) {
    return fromValidationError(error)
  }
  if (kind === 'login' && status === 401) {
    return { general: WRONG_CREDENTIALS_MESSAGE }
  }
  if (kind === 'register' && status === 409) {
    if (readTitle(error) === DISPLAY_NAME_TAKEN_TITLE) {
      return { displayName: DISPLAY_NAME_TAKEN_MESSAGE }
    }
    return { general: EMAIL_TAKEN_MESSAGE, emailTaken: true }
  }
  return { general: GENERIC_MESSAGE }
}

// Used when the request itself failed (network down, unreadable answer).
export function networkFormErrors(): FormErrors {
  return { general: GENERIC_MESSAGE }
}
