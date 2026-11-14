import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import HomePage from './HomePage'

describe('HomePage', () => {
  it('shows exactly one level-one heading', () => {
    render(<HomePage />)

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  })

  it('shows the welcome sentence', () => {
    render(<HomePage />)

    expect(
      screen.getByText('Find a new friend or help a pet find a loving home.'),
    ).toBeInTheDocument()
  })
})
