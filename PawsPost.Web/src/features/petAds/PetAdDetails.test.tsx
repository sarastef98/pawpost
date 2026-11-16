import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import PetAdDetails from './PetAdDetails'
import type { PetAdDetails as Ad } from './petAdDetailsData'

function ad(overrides: Partial<Ad> = {}): Ad {
  return {
    id: 'ad-1',
    title: 'Friendly Rex',
    type: 'Sale',
    species: 'Dog',
    breed: 'Labrador',
    ageInMonths: 27,
    description: 'A calm dog.',
    price: 150.5,
    location: 'Skopje',
    creator: null,
    ...overrides,
  }
}

describe('PetAdDetails', () => {
  it('shows the title as the main heading', () => {
    render(<PetAdDetails ad={ad()} />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'Friendly Rex' }),
    ).toBeInTheDocument()
  })

  it('shows all fields of a Sale ad', () => {
    render(<PetAdDetails ad={ad()} />)

    expect(screen.getByText('Sale')).toBeInTheDocument()
    expect(screen.getByText('€150.50')).toBeInTheDocument()
    expect(screen.getByText('Species')).toBeInTheDocument()
    expect(screen.getByText('Dog')).toBeInTheDocument()
    expect(screen.getByText('Labrador')).toBeInTheDocument()
    expect(screen.getByText('2 years 3 months')).toBeInTheDocument()
    expect(screen.getByText('Skopje')).toBeInTheDocument()
    expect(screen.getByText('A calm dog.')).toBeInTheDocument()
  })

  it('shows an Adoption ad with the Adoption badge and no price', () => {
    render(<PetAdDetails ad={ad({ type: 'Adoption', price: 99 })} />)

    expect(screen.getByText('Adoption')).toBeInTheDocument()
    expect(screen.queryByText(/€/)).not.toBeInTheDocument()
  })

  it('shows no price for a Sale ad without a price', () => {
    render(<PetAdDetails ad={ad({ price: null })} />)

    expect(screen.queryByText(/€/)).not.toBeInTheDocument()
  })

  it('hides the breed and location rows when they are null', () => {
    render(<PetAdDetails ad={ad({ breed: null, location: null })} />)

    expect(screen.queryByText('Breed')).not.toBeInTheDocument()
    expect(screen.queryByText('Location')).not.toBeInTheDocument()
    expect(screen.getByText('Species')).toBeInTheDocument()
  })

  it('hides the age row when the age is unknown', () => {
    render(<PetAdDetails ad={ad({ ageInMonths: null })} />)

    expect(screen.queryByText('Age')).not.toBeInTheDocument()
  })

  it('uses the species as heading when the title is empty', () => {
    render(<PetAdDetails ad={ad({ title: '  ' })} />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'Dog' }),
    ).toBeInTheDocument()
  })

  it('shows HTML characters in the description as text', () => {
    render(<PetAdDetails ad={ad({ description: '<b>bold</b> <img src=x>' })} />)

    expect(screen.getByText('<b>bold</b> <img src=x>')).toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('does not show any contact details', () => {
    render(<PetAdDetails ad={ad()} />)

    expect(screen.queryByText(/@/)).not.toBeInTheDocument()
    expect(screen.queryByText(/phone|email/i)).not.toBeInTheDocument()
  })
})
