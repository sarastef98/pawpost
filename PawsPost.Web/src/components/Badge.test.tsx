import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Badge from './Badge'

describe('Badge', () => {
  it('shows the Adoption label for the adoption variant', () => {
    render(<Badge variant="adoption" />)

    expect(screen.getByText('Adoption')).toBeInTheDocument()
  })

  it('shows the Sale label for the sale variant', () => {
    render(<Badge variant="sale" />)

    expect(screen.getByText('Sale')).toBeInTheDocument()
  })

  it('shows the Closed label for the closed variant', () => {
    render(<Badge variant="closed" />)

    expect(screen.getByText('Closed')).toBeInTheDocument()
  })
})
