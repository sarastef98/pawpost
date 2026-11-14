import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import PetAdFilters from './PetAdFilters'

describe('PetAdFilters', () => {
  it('has a labelled Type select and a labelled Species field limited to 50 characters', () => {
    render(<PetAdFilters type="" species="" onApply={vi.fn()} />)

    expect(screen.getByLabelText('Type')).toBeInTheDocument()
    expect(screen.getByLabelText('Species')).toHaveAttribute('maxlength', '50')
    expect(screen.getByLabelText('Species')).toHaveAccessibleDescription(
      'For example Dog or Cat',
    )
  })

  it('fills the fields from the given filters', () => {
    render(<PetAdFilters type="Sale" species="Dog" onApply={vi.fn()} />)

    expect(screen.getByLabelText('Type')).toHaveValue('Sale')
    expect(screen.getByLabelText('Species')).toHaveValue('Dog')
  })

  it('does not apply anything until the Apply button is pressed', async () => {
    const onApply = vi.fn()
    render(<PetAdFilters type="" species="" onApply={onApply} />)

    await userEvent.type(screen.getByLabelText('Species'), 'Dog')

    expect(onApply).not.toHaveBeenCalled()
  })

  it('applies the chosen type and the trimmed species', async () => {
    const onApply = vi.fn()
    render(<PetAdFilters type="" species="" onApply={onApply} />)

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'Sale')
    await userEvent.type(screen.getByLabelText('Species'), '  Dog  ')
    await userEvent.click(screen.getByRole('button', { name: 'Apply filters' }))

    expect(onApply).toHaveBeenCalledWith('Sale', 'Dog')
  })

  it('applies when Enter is pressed in the species field', async () => {
    const onApply = vi.fn()
    render(<PetAdFilters type="" species="" onApply={onApply} />)

    await userEvent.type(screen.getByLabelText('Species'), 'Cat{Enter}')

    expect(onApply).toHaveBeenCalledWith('', 'Cat')
  })
})
