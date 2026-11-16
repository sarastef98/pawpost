import type { components } from '../../api/schema'

export type AssistantSearchBody =
  components['schemas']['AssistantSearchRequest']

// Mirrors AssistantSearchRequest.QuestionMaxLength on the server.
export const QUESTION_MAX_LENGTH = 500

export const EMPTY_QUESTION_MESSAGE = 'Please type a question.'
export const TOO_LONG_QUESTION_MESSAGE =
  'Your question can be at most 500 characters.'

// Convenience check only, the server decides. Spaces around the question are
// ignored, like on the server. Returns an error message or null.
export function validateQuestion(value: string): string | null {
  const trimmed = value.trim()
  if (trimmed === '') return EMPTY_QUESTION_MESSAGE
  if (trimmed.length > QUESTION_MAX_LENGTH) return TOO_LONG_QUESTION_MESSAGE
  return null
}

export function buildSearchBody(value: string): AssistantSearchBody {
  return { question: value.trim() }
}
