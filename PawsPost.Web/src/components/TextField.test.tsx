import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import TextField from './TextField'

describe('TextField', () => {
  it('links the label to the input', () => {
    render(<TextField label="Email" />)

    expect(screen.getByLabelText('Email')).toBeInTheDocument()
  })

  it('updates the value when the user types', async () => {
    render(<TextField label="Email" />)

    await userEvent.type(screen.getByLabelText('Email'), 'hello')

    expect(screen.getByLabelText('Email')).toHaveValue('hello')
  })

  it('shows helper text linked to the input', () => {
    render(<TextField label="Email" helperText="We never share it" />)

    expect(screen.getByText('We never share it')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription(
      'We never share it',
    )
    expect(screen.getByLabelText('Email')).not.toHaveAttribute('aria-invalid')
  })

  it('shows error text and marks the input invalid', () => {
    render(<TextField label="Email" errorText="Email is required" />)

    expect(screen.getByText('Email is required')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInvalid()
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription(
      'Email is required',
    )
  })

  it('shows the error instead of the helper text when both are given', () => {
    render(
      <TextField
        label="Email"
        helperText="We never share it"
        errorText="Email is required"
      />,
    )

    expect(screen.queryByText('We never share it')).not.toBeInTheDocument()
    expect(screen.getByText('Email is required')).toBeInTheDocument()
  })

  it('keeps the caller aria-invalid when there is no error text', () => {
    render(<TextField label="Email" aria-invalid="true" />)

    expect(screen.getByLabelText('Email')).toBeInvalid()
  })

  it('marks the input invalid when there is error text even if the caller says it is valid', () => {
    render(
      <TextField
        label="Email"
        errorText="Email is required"
        aria-invalid="false"
      />,
    )

    expect(screen.getByLabelText('Email')).toBeInvalid()
  })

  it('keeps the helper text linked when a caller passes its own aria-describedby', () => {
    render(
      <>
        <p id="extra-hint">Use your work address</p>
        <TextField
          label="Email"
          helperText="We never share it"
          aria-describedby="extra-hint"
        />
      </>,
    )

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription(
      'Use your work address We never share it',
    )
  })

  it('can be focused with the keyboard', async () => {
    render(<TextField label="Email" />)

    await userEvent.tab()

    expect(screen.getByLabelText('Email')).toHaveFocus()
  })
})
