import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import Select from './Select'

const options = [
  { value: '', label: 'All types' },
  { value: 'Adoption', label: 'Adoption' },
  { value: 'Sale', label: 'Sale' },
]

describe('Select', () => {
  it('links the label to the select', () => {
    render(<Select label="Type" options={options} />)

    expect(screen.getByLabelText('Type')).toBeInTheDocument()
  })

  it('shows every option', () => {
    render(<Select label="Type" options={options} />)

    expect(screen.getAllByRole('option')).toHaveLength(3)
    expect(
      screen.getByRole('option', { name: 'All types' }),
    ).toBeInTheDocument()
  })

  it('changes the value when the user picks an option', async () => {
    render(<Select label="Type" options={options} />)

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'Sale')

    expect(screen.getByLabelText('Type')).toHaveValue('Sale')
  })

  it('shows helper text linked to the select', () => {
    render(<Select label="Type" options={options} helperText="Pick one" />)

    expect(screen.getByLabelText('Type')).toHaveAccessibleDescription(
      'Pick one',
    )
    expect(screen.getByLabelText('Type')).not.toHaveAttribute('aria-invalid')
  })

  it('shows error text and marks the select invalid', () => {
    render(<Select label="Type" options={options} errorText="Pick a type" />)

    expect(screen.getByLabelText('Type')).toBeInvalid()
    expect(screen.getByLabelText('Type')).toHaveAccessibleDescription(
      'Pick a type',
    )
  })

  it('shows the error instead of the helper text when both are given', () => {
    render(
      <Select
        label="Type"
        options={options}
        helperText="Pick one"
        errorText="Pick a type"
      />,
    )

    expect(screen.queryByText('Pick one')).not.toBeInTheDocument()
    expect(screen.getByText('Pick a type')).toBeInTheDocument()
  })

  it('keeps the caller aria-describedby next to the helper text', () => {
    render(
      <>
        <p id="extra-hint">Extra hint</p>
        <Select
          label="Type"
          options={options}
          helperText="Pick one"
          aria-describedby="extra-hint"
        />
      </>,
    )

    expect(screen.getByLabelText('Type')).toHaveAccessibleDescription(
      'Extra hint Pick one',
    )
  })

  it('can be focused with the keyboard', async () => {
    render(<Select label="Type" options={options} />)

    await userEvent.tab()

    expect(screen.getByLabelText('Type')).toHaveFocus()
  })
})
