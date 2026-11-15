import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import NotFoundPage from './NotFoundPage'

function renderPage() {
  render(
    <MemoryRouter>
      <NotFoundPage />
    </MemoryRouter>,
  )
}

describe('NotFoundPage', () => {
  it('shows exactly one level one heading named Page not found', () => {
    renderPage()

    const headings = screen.getAllByRole('heading', { level: 1 })
    expect(headings).toHaveLength(1)
    expect(headings[0]).toHaveTextContent('Page not found')
  })

  it('shows the friendly message', () => {
    renderPage()

    expect(
      screen.getByText(
        "We could not find that page. Let's get you back to the ads.",
      ),
    ).toBeInTheDocument()
  })

  it('shows no status code or error text', () => {
    renderPage()

    expect(screen.queryByText(/404/)).not.toBeInTheDocument()
    expect(screen.queryByText(/error/i)).not.toBeInTheDocument()
  })

  it('has a link back to the home page', () => {
    renderPage()

    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute(
      'href',
      '/',
    )
  })

  it('sets the document title to Page not found', async () => {
    renderPage()

    await expect.poll(() => document.title).toBe('Page not found')
  })

  it('lets the user reach the link with the keyboard', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.tab()

    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveFocus()
  })
})
