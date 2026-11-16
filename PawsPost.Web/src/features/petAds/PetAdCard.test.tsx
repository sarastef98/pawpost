import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import PetAdCard from './PetAdCard'
import type { PetAdSummary } from './petAdListQuery'

function renderCard(overrides: Partial<PetAdSummary> = {}) {
  const ad: PetAdSummary = {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Adoption',
    price: null,
    location: 'Skopje',
    creator: null,
    ...overrides,
  }
  render(
    <MemoryRouter>
      <PetAdCard ad={ad} />
    </MemoryRouter>,
  )
}

describe('PetAdCard', () => {
  it('shows the title, the Adoption badge and the location', () => {
    renderCard()

    expect(
      screen.getByRole('heading', { level: 3, name: 'Friendly Rex' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Adoption')).toBeInTheDocument()
    expect(screen.getByText('Skopje')).toBeInTheDocument()
  })

  it('shows the Sale badge and the euro price on a Sale card', () => {
    renderCard({ type: 'Sale', price: 150.5 })

    expect(screen.getByText('Sale')).toBeInTheDocument()
    expect(screen.getByText('€150.50')).toBeInTheDocument()
  })

  it('shows a very long price in full', () => {
    renderCard({ type: 'Sale', price: 1e21 })

    expect(
      screen.getByText('€1,000,000,000,000,000,000,000.00'),
    ).toBeInTheDocument()
  })

  it('shows the price with two decimals and thousands separators', () => {
    renderCard({ type: 'Sale', price: 1200 })

    expect(screen.getByText('€1,200.00')).toBeInTheDocument()
  })

  it('shows no price on an Adoption card even when a price is given', () => {
    renderCard({ type: 'Adoption', price: 100 })

    expect(screen.queryByText(/€/)).not.toBeInTheDocument()
  })

  it('shows no price for a Sale ad without a price', () => {
    renderCard({ type: 'Sale', price: null })

    expect(screen.queryByText(/€|null|NaN/)).not.toBeInTheDocument()
  })

  it('shows an unexpected type with the Adoption badge and no price', () => {
    renderCard({ type: 'Swap', price: 100 })

    expect(screen.getByText('Adoption')).toBeInTheDocument()
    expect(screen.queryByText(/€/)).not.toBeInTheDocument()
  })

  it('shows no location text when the location is missing', () => {
    renderCard({ location: null })

    expect(screen.queryByText(/null/)).not.toBeInTheDocument()
    expect(screen.queryByText('Skopje')).not.toBeInTheDocument()
  })

  it('links to the ad details page', () => {
    renderCard({ id: 'ad-42' })

    expect(screen.getByRole('link', { name: /Friendly Rex/ })).toHaveAttribute(
      'href',
      '/pet-ads/ad-42',
    )
  })
})

describe('PetAdCard creator', () => {
  it('shows the creator name as plain text', () => {
    renderCard({ creator: { id: 'c1', displayName: 'Anna' } })

    expect(screen.getByText('By Anna')).toBeInTheDocument()
  })

  it('has exactly one link, to the ad, and the name is not a link', () => {
    renderCard({ id: 'ad-7', creator: { id: 'c1', displayName: 'Anna' } })

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(1)
    expect(links[0]).toHaveAttribute('href', '/pet-ads/ad-7')
    expect(links[0]).toHaveTextContent('By Anna')
  })

  it('hides the creator line when the creator is missing and keeps the rest', () => {
    renderCard({ creator: null })

    expect(screen.queryByText(/^By /)).not.toBeInTheDocument()
    expect(screen.getByText('Friendly Rex')).toBeInTheDocument()
    expect(screen.getByText('Skopje')).toBeInTheDocument()
  })

  it('shows a name with HTML characters as text', () => {
    renderCard({ creator: { id: 'c1', displayName: '<b>Anna</b>' } })

    expect(screen.getByText('By <b>Anna</b>')).toBeInTheDocument()
    expect(document.querySelector('b')).toBeNull()
  })
})
