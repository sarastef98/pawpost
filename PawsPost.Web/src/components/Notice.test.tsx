import { act, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import Notice from './Notice'

function StateProbe() {
  const state: unknown = useLocation().state
  return <p>Router state: {JSON.stringify(state ?? null)}</p>
}

// Notice fills its text on the next tick. Letting one timer tick pass first
// means a check for an empty region fails if any text appears.
async function nextTick() {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0))
  })
}

function renderNotice(state?: unknown) {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/page', state }]}>
      <Routes>
        <Route
          path="/page"
          element={
            <>
              <Notice />
              <StateProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('Notice', () => {
  it('shows the router-state notice in a status region', async () => {
    renderNotice({ notice: 'Your ad was closed.' })

    expect(await screen.findByText('Your ad was closed.')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Your ad was closed.')
  })

  it('clears the router state after reading the notice', async () => {
    renderNotice({ notice: 'Your ad was closed.' })

    expect(await screen.findByText('Router state: null')).toBeInTheDocument()
    expect(await screen.findByText('Your ad was closed.')).toBeInTheDocument()
  })

  it('shows an empty status region without router state', async () => {
    renderNotice()

    await nextTick()

    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('shows nothing when the router state has no text notice', async () => {
    renderNotice({ notice: 5 })

    await nextTick()

    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})
