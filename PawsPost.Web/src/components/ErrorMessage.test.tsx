import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import ErrorMessage from './ErrorMessage'

describe('ErrorMessage', () => {
  it('shows the message it is given with an alert role', () => {
    render(<ErrorMessage message="Could not load ads" />)

    expect(screen.getByRole('alert')).toHaveTextContent('Could not load ads')
  })

  it('shows no retry button when onRetry is not given', () => {
    render(<ErrorMessage message="Could not load ads" />)

    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('calls onRetry when the retry button is clicked', async () => {
    const onRetry = vi.fn()
    render(<ErrorMessage message="Could not load ads" onRetry={onRetry} />)

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(onRetry).toHaveBeenCalledTimes(1)
  })

  it('shows only the message and no technical details', () => {
    render(<ErrorMessage message="Could not load ads" />)

    expect(screen.getByRole('alert')).toHaveTextContent(/^Could not load ads$/)
  })
})
