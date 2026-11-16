import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import type { PetAdListData } from '../petAds/petAdListQuery'
import CreatorAdList from './CreatorAdList'

function item(id: string, title: string) {
  return {
    id,
    title,
    type: 'Adoption',
    price: null,
    location: 'Skopje',
    creator: { id: 'creator-1', displayName: 'Anna' },
  }
}

function renderList(
  ads: Partial<PetAdListData> = {},
  page = 1,
  onPageChange = vi.fn(),
) {
  const data: PetAdListData = {
    items: [item('a', 'First'), item('b', 'Second')],
    totalCount: 45,
    page,
    pageSize: 20,
    ...ads,
  }
  render(
    <MemoryRouter>
      <CreatorAdList
        displayName="Anna"
        ads={data}
        page={page}
        onPageChange={onPageChange}
      />
    </MemoryRouter>,
  )
  return onPageChange
}

describe('CreatorAdList', () => {
  it('shows the Ads heading and one card per ad in the given order', () => {
    renderList()

    expect(
      screen.getByRole('heading', { level: 2, name: 'Ads' }),
    ).toBeInTheDocument()
    const titles = screen.getAllByRole('heading', { level: 3 })
    expect(titles.map((t) => t.textContent)).toEqual(['First', 'Second'])
  })

  it('shows the empty state with a link to all ads and no pager', () => {
    renderList({ items: [], totalCount: 0 })

    expect(
      screen.getByText('Anna has no ads right now. Please check back soon.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Browse all ads' }),
    ).toHaveAttribute('href', '/')
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })

  it('disables Previous on the first page and shows the page count', () => {
    renderList({}, 1)

    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument()
  })

  it('disables Next on the last page', () => {
    renderList({}, 3)

    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('asks for the next and the previous page', async () => {
    const user = userEvent.setup()
    const onPageChange = renderList({}, 2)

    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(screen.getByRole('button', { name: 'Previous' }))

    expect(onPageChange).toHaveBeenNthCalledWith(1, 3)
    expect(onPageChange).toHaveBeenNthCalledWith(2, 1)
  })
})
