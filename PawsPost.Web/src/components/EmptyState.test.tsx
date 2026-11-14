import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Button from './Button'
import EmptyState from './EmptyState'

describe('EmptyState', () => {
  it('shows the message it is given', () => {
    render(<EmptyState message="No ads yet" />)

    expect(screen.getByText('No ads yet')).toBeInTheDocument()
  })

  it('shows the action when one is given', () => {
    render(
      <EmptyState message="No ads yet">
        <Button>Post an ad</Button>
      </EmptyState>,
    )

    expect(
      screen.getByRole('button', { name: 'Post an ad' }),
    ).toBeInTheDocument()
  })

  it('shows no button when no action is given', () => {
    render(<EmptyState message="No ads yet" />)

    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
