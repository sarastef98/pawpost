import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import StyleGuidePage from './StyleGuidePage'

describe('StyleGuidePage', () => {
  it('shows a heading for each component group', () => {
    render(<StyleGuidePage />)

    for (const name of [
      'Button',
      'TextField',
      'Card',
      'Badge',
      'Spinner',
      'EmptyState',
      'ErrorMessage',
    ]) {
      expect(
        screen.getByRole('heading', { level: 2, name }),
      ).toBeInTheDocument()
    }
  })

  it('shows primary, secondary and danger buttons', () => {
    render(<StyleGuidePage />)

    expect(screen.getByRole('button', { name: 'Primary' })).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Secondary' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Danger' })).toBeInTheDocument()
  })

  it('shows a text field with a label only', () => {
    render(<StyleGuidePage />)

    const field = screen.getByLabelText('Pet name')
    expect(field).not.toHaveAttribute('aria-invalid')
    expect(field).not.toHaveAttribute('aria-describedby')
  })

  it('shows a text field with helper text', () => {
    render(<StyleGuidePage />)

    expect(screen.getByLabelText('Breed')).toHaveAccessibleDescription(
      'For example, Labrador.',
    )
  })

  it('shows a text field with error text marked as invalid', () => {
    render(<StyleGuidePage />)

    const field = screen.getByLabelText('Location')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(field).toHaveAccessibleDescription('Please add a location.')
  })

  it('shows the card', () => {
    render(<StyleGuidePage />)

    expect(
      screen.getByText('Meet Luna, a friendly cat looking for a home.'),
    ).toBeInTheDocument()
  })

  it('shows the Adoption and Sale badges', () => {
    render(<StyleGuidePage />)

    expect(screen.getByText('Adoption')).toBeInTheDocument()
    expect(screen.getByText('Sale')).toBeInTheDocument()
  })

  it('shows a loading status', () => {
    render(<StyleGuidePage />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading')
  })

  it('shows the empty state message with its action', () => {
    render(<StyleGuidePage />)

    expect(screen.getByText('No pets here yet.')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Post an ad' }),
    ).toBeInTheDocument()
  })

  it('shows an error message without a retry button', () => {
    render(<StyleGuidePage />)

    const alert = screen
      .getAllByRole('alert')
      .find((el) => el.textContent?.includes('Something went wrong.'))
    expect(alert).toBeDefined()
    expect(alert).not.toHaveTextContent('Try again')
  })

  it('shows an error message with a retry button', () => {
    render(<StyleGuidePage />)

    const alert = screen
      .getAllByRole('alert')
      .find((el) => el.textContent?.includes('We could not load the pets.'))
    expect(alert).toBeDefined()
    expect(alert).toHaveTextContent('Try again')
    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })

  it('lets the sample buttons be reached with the Tab key', async () => {
    const user = userEvent.setup()
    render(<StyleGuidePage />)

    await user.tab()
    expect(screen.getByRole('button', { name: 'Primary' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: 'Secondary' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: 'Danger' })).toHaveFocus()
  })
})
