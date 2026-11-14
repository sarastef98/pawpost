import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Card from './Card'

describe('Card', () => {
  it('renders its children', () => {
    render(
      <Card>
        <p>Rex, a friendly dog</p>
      </Card>,
    )

    expect(screen.getByText('Rex, a friendly dog')).toBeInTheDocument()
  })
})
