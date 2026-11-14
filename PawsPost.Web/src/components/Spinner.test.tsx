import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Spinner from './Spinner'

describe('Spinner', () => {
  it('exposes a status role with the Loading text', () => {
    render(<Spinner />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading')
  })
})
