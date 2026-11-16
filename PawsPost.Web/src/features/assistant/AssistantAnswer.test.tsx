import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { sampleAd } from '../../test/apiResponses'
import { toPetAdDetails } from '../petAds/petAdDetailsData'
import AssistantAnswer from './AssistantAnswer'

function ads(...overrides: Record<string, unknown>[]) {
  return overrides.map((o) => toPetAdDetails(sampleAd(o))!)
}

function renderAnswer(answer: string, list: ReturnType<typeof ads>) {
  render(
    <MemoryRouter>
      <AssistantAnswer answer={answer} ads={list} />
    </MemoryRouter>,
  )
}

describe('AssistantAnswer', () => {
  it('shows the answer text under an Answer heading', () => {
    renderAnswer('Rex sounds like a good fit.', ads({}))

    expect(
      screen.getByRole('heading', { level: 2, name: 'Answer' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Rex sounds like a good fit.')).toBeInTheDocument()
  })

  it('shows one card per ad in the given order', () => {
    renderAnswer(
      'Here you go.',
      ads(
        { id: 'a', title: 'Second best' },
        { id: 'b', title: 'First by order' },
        { id: 'c', title: 'Third' },
      ),
    )

    expect(
      screen.getByRole('heading', { level: 2, name: 'Matching ads' }),
    ).toBeInTheDocument()
    const items = within(screen.getByRole('list')).getAllByRole('listitem')
    expect(items).toHaveLength(3)
    expect(
      items.map((item) => within(item).getByRole('heading').textContent),
    ).toEqual(['Second best', 'First by order', 'Third'])
  })

  it('shows the empty state with a link to all ads and no list when there are no ads', () => {
    renderAnswer('Sorry, nothing found.', [])

    expect(screen.getByText('Sorry, nothing found.')).toBeInTheDocument()
    expect(
      screen.getByText(
        'No ads match your question yet. Try describing the pet in other words.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Browse all ads' }),
    ).toHaveAttribute('href', '/')
    expect(screen.queryByRole('list')).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Matching ads' })).toBeNull()
  })

  it('does not show the empty state when there are ads', () => {
    renderAnswer('Found one.', ads({}))

    expect(screen.queryByRole('link', { name: 'Browse all ads' })).toBeNull()
  })
})

describe('AssistantAnswer creator', () => {
  it('shows "By <name>" as plain text and keeps one link per card', () => {
    renderAnswer(
      'Here.',
      ads({ id: 'ad-9', creator: { id: 'c1', displayName: 'Anna' } }),
    )

    expect(screen.getByText('By Anna')).toBeInTheDocument()
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(1)
    expect(links[0]).toHaveAttribute('href', '/pet-ads/ad-9')
  })

  it('shows no creator line and the rest of the card when the creator is missing', () => {
    renderAnswer('Here.', ads({ creator: undefined }))

    expect(screen.queryByText(/^By /)).toBeNull()
    expect(screen.getByText('Friendly Rex')).toBeInTheDocument()
    expect(screen.getByText('Skopje')).toBeInTheDocument()
  })
})
