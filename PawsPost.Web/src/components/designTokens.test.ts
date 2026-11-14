import { describe, expect, it } from 'vitest'

// Read the component source files as plain text (test files are excluded).
const sources = import.meta.glob(['./*.tsx', '!./*.test.tsx'], {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

describe('shared components', () => {
  it('are found by the guard check', () => {
    expect(Object.keys(sources).length).toBeGreaterThanOrEqual(7)
  })

  it('contain no hard-coded hex colors', () => {
    const offenders = Object.entries(sources)
      .filter(([, code]) => /#[0-9a-fA-F]{3,8}\b/.test(code))
      .map(([file]) => file)

    expect(offenders).toEqual([])
  })

  it('contain no arbitrary-value Tailwind classes', () => {
    const offenders = Object.entries(sources)
      .filter(([, code]) => /[a-z-]+-\[[^\]]*\]/.test(code))
      .map(([file]) => file)

    expect(offenders).toEqual([])
  })
})
