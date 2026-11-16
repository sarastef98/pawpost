import { describe, expect, it } from 'vitest'
import { BAD_QUESTION_MESSAGE, mapQuestionError } from './assistantErrors'

describe('mapQuestionError', () => {
  it.each(['question', 'Question', '$.question'])(
    'shows the message under the key %s',
    (key) => {
      expect(mapQuestionError({ errors: { [key]: ['Too long.'] } })).toBe(
        'Too long.',
      )
    },
  )

  it('shows messages under an unknown key', () => {
    expect(mapQuestionError({ errors: { other: ['Odd.'] } })).toBe('Odd.')
  })

  it('shows messages under an empty key', () => {
    expect(mapQuestionError({ errors: { '': ['Body is required.'] } })).toBe(
      'Body is required.',
    )
  })

  it('joins several messages into one', () => {
    expect(
      mapQuestionError({
        errors: { question: ['First.', 'Second.'], '$.x': ['Third.'] },
      }),
    ).toBe('First. Second. Third.')
  })

  it('ignores blank and non-text messages', () => {
    expect(mapQuestionError({ errors: { question: ['  ', 5, 'Real.'] } })).toBe(
      'Real.',
    )
  })

  it.each([
    ['an empty object', {}],
    ['null', null],
    ['undefined', undefined],
    ['text', 'oops'],
    ['errors that is not an object', { errors: 'bad' }],
    ['values that are not lists', { errors: { question: 'bad' } }],
    ['an empty list', { errors: { question: [] } }],
  ])('uses the fallback for %s', (_name, body) => {
    expect(mapQuestionError(body)).toBe(BAD_QUESTION_MESSAGE)
  })
})
