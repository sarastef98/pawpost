import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import App from './App'

function renderAt(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <App />
    </MemoryRouter>,
  )
}

describe('App', () => {
  it('shows the home page inside the layout at the home route', () => {
    renderAt('/')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
  })

  it('shows the style guide inside the layout at the style guide route', () => {
    renderAt('/styleguide')

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 1, name: 'Style guide' }),
    ).toBeInTheDocument()
  })

  it('goes from the style guide back to the home page when the logo is clicked', async () => {
    const user = userEvent.setup()
    renderAt('/styleguide')

    await user.click(screen.getByRole('link', { name: 'PawsPost' }))

    expect(
      screen.getByRole('heading', { level: 1, name: 'Welcome to PawsPost' }),
    ).toBeInTheDocument()
  })
})
