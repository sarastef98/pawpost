import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import HomePage from './HomePage'

// The home page lists ads. Tests answer with an empty list at the API client
// boundary.
vi.mock('../../api/client', () => ({
  client: {
    GET: vi.fn(() =>
      Promise.resolve({
        data: { items: [], totalCount: 0, page: 1, pageSize: 20 },
        error: undefined,
        response: { status: 200, ok: true },
      }),
    ),
  },
}))

function renderHome() {
  render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  )
}

describe('HomePage', () => {
  it('shows exactly one level-one heading', () => {
    renderHome()

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  })

  it('shows the welcome sentence', () => {
    renderHome()

    expect(
      screen.getByText('Find a new friend or help a pet find a loving home.'),
    ).toBeInTheDocument()
  })
})
