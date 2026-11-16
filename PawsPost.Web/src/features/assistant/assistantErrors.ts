export const SEARCH_FAILED_MESSAGE =
  'The assistant could not answer right now. Please try again.'
export const BAD_QUESTION_MESSAGE = 'Please check your question and try again.'

// Reads all text messages from a ValidationProblemDetails body.
function readMessages(error: unknown): string[] {
  if (typeof error !== 'object' || error === null || !('errors' in error)) {
    return []
  }
  const errors = error.errors
  if (typeof errors !== 'object' || errors === null) return []
  const result: string[] = []
  for (const value of Object.values(errors)) {
    if (!Array.isArray(value)) continue
    for (const item of value) {
      if (typeof item === 'string' && item.trim() !== '') result.push(item)
    }
  }
  return result
}

// Turns a 400 body (ValidationProblemDetails) into one message for the only
// field. Messages under any key (question, Question, $.question, unknown or
// empty keys) are shown next to the field, as sent by the server. Without a
// usable message the friendly fallback is used.
export function mapQuestionError(error: unknown): string {
  const messages = readMessages(error)
  return messages.length > 0 ? messages.join(' ') : BAD_QUESTION_MESSAGE
}
