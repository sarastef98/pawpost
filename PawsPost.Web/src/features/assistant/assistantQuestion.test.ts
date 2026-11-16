import { describe, expect, it } from 'vitest'
import {
  EMPTY_QUESTION_MESSAGE,
  QUESTION_MAX_LENGTH,
  TOO_LONG_QUESTION_MESSAGE,
  buildSearchBody,
  validateQuestion,
} from './assistantQuestion'

describe('validateQuestion', () => {
  it('rejects an empty question', () => {
    expect(validateQuestion('')).toBe(EMPTY_QUESTION_MESSAGE)
  })

  it('rejects a question with only whitespace', () => {
    expect(validateQuestion('   \t ')).toBe(EMPTY_QUESTION_MESSAGE)
  })

  it('accepts a question of exactly 500 characters', () => {
    expect(validateQuestion('a'.repeat(QUESTION_MAX_LENGTH))).toBeNull()
  })

  it('rejects a question of 501 characters', () => {
    expect(validateQuestion('a'.repeat(QUESTION_MAX_LENGTH + 1))).toBe(
      TOO_LONG_QUESTION_MESSAGE,
    )
  })

  it('does not count surrounding spaces', () => {
    expect(
      validateQuestion(`  ${'a'.repeat(QUESTION_MAX_LENGTH)}  `),
    ).toBeNull()
  })

  it('accepts a normal question', () => {
    expect(validateQuestion('A calm dog for children')).toBeNull()
  })
})

describe('buildSearchBody', () => {
  it('trims the question', () => {
    expect(buildSearchBody('  a calm dog \n')).toEqual({
      question: 'a calm dog',
    })
  })
})
