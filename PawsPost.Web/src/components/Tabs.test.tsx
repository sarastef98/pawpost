import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import Tabs from './Tabs'

function renderTabs(count = 3) {
  const contents = Array.from({ length: count }, (_, i) =>
    vi.fn(() => <p>Content {i + 1}</p>),
  )
  const items = contents.map((content, i) => ({
    id: `item-${i + 1}`,
    label: `Tab ${i + 1}`,
    content,
  }))
  render(<Tabs label="Test tabs" items={items} />)
  return { contents }
}

describe('Tabs', () => {
  it('renders a tablist with the given name, one tab per item and one visible panel', () => {
    renderTabs()

    expect(screen.getByRole('tablist', { name: 'Test tabs' })).toBeVisible()
    expect(screen.getAllByRole('tab')).toHaveLength(3)
    expect(screen.getAllByRole('tabpanel')).toHaveLength(1)
    expect(screen.getByRole('tabpanel')).toBeVisible()
  })

  it('selects the first tab by default', () => {
    renderTabs()

    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('tab', { name: 'Tab 2' })).toHaveAttribute(
      'aria-selected',
      'false',
    )
    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveAttribute(
      'aria-selected',
      'false',
    )
  })

  it('links each tab to its panel and the panel back to the tab', () => {
    renderTabs()
    const tab = screen.getByRole('tab', { name: 'Tab 1' })
    const panel = screen.getByRole('tabpanel')

    expect(tab.getAttribute('aria-controls')).toBe(panel.id)
    expect(panel.getAttribute('aria-labelledby')).toBe(tab.id)
    expect(panel).toHaveAccessibleName('Tab 1')
  })

  it('gives only the selected tab a tab stop', () => {
    renderTabs()

    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveAttribute(
      'tabindex',
      '0',
    )
    expect(screen.getByRole('tab', { name: 'Tab 2' })).toHaveAttribute(
      'tabindex',
      '-1',
    )
    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveAttribute(
      'tabindex',
      '-1',
    )
  })

  it('selects a tab and shows its panel when it is clicked', async () => {
    const user = userEvent.setup()
    renderTabs()

    await user.click(screen.getByRole('tab', { name: 'Tab 2' }))

    expect(screen.getByRole('tab', { name: 'Tab 2' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByText('Content 2')).toBeVisible()
    expect(screen.queryByText('Content 1')).toBeNull()
  })

  it('moves focus to the next tab and selects it on ArrowRight', async () => {
    const user = userEvent.setup()
    renderTabs()
    screen.getByRole('tab', { name: 'Tab 1' }).focus()

    await user.keyboard('{ArrowRight}')

    const next = screen.getByRole('tab', { name: 'Tab 2' })
    expect(next).toHaveFocus()
    expect(next).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Content 2')).toBeVisible()
  })

  it('moves focus to the previous tab and selects it on ArrowLeft', async () => {
    const user = userEvent.setup()
    renderTabs()
    await user.click(screen.getByRole('tab', { name: 'Tab 2' }))

    await user.keyboard('{ArrowLeft}')

    const previous = screen.getByRole('tab', { name: 'Tab 1' })
    expect(previous).toHaveFocus()
    expect(previous).toHaveAttribute('aria-selected', 'true')
  })

  it('wraps from the last tab to the first on ArrowRight', async () => {
    const user = userEvent.setup()
    renderTabs()
    await user.click(screen.getByRole('tab', { name: 'Tab 3' }))

    await user.keyboard('{ArrowRight}')

    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveFocus()
    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('wraps from the first tab to the last on ArrowLeft', async () => {
    const user = userEvent.setup()
    renderTabs()
    screen.getByRole('tab', { name: 'Tab 1' }).focus()

    await user.keyboard('{ArrowLeft}')

    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveFocus()
    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('selects and focuses the first tab on Home', async () => {
    const user = userEvent.setup()
    renderTabs()
    await user.click(screen.getByRole('tab', { name: 'Tab 3' }))

    await user.keyboard('{Home}')

    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveFocus()
    expect(screen.getByRole('tab', { name: 'Tab 1' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('selects and focuses the last tab on End', async () => {
    const user = userEvent.setup()
    renderTabs()
    screen.getByRole('tab', { name: 'Tab 1' }).focus()

    await user.keyboard('{End}')

    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveFocus()
    expect(screen.getByRole('tab', { name: 'Tab 3' })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('moves focus from the selected tab into the panel with the Tab key', async () => {
    const user = userEvent.setup()
    renderTabs()
    screen.getByRole('tab', { name: 'Tab 1' }).focus()

    await user.tab()

    expect(screen.getByRole('tabpanel')).toHaveFocus()
  })

  it('renders only the selected panel content', async () => {
    const user = userEvent.setup()
    const { contents } = renderTabs()

    expect(contents[0]).toHaveBeenCalled()
    expect(contents[1]).not.toHaveBeenCalled()
    expect(contents[2]).not.toHaveBeenCalled()

    await user.click(screen.getByRole('tab', { name: 'Tab 2' }))

    expect(contents[1]).toHaveBeenCalled()
    expect(contents[2]).not.toHaveBeenCalled()
  })

  it('renders every tab as a button', () => {
    renderTabs()

    for (const tab of screen.getAllByRole('tab')) {
      expect(tab.tagName).toBe('BUTTON')
    }
  })

  it('keeps focus and selection on a single tab when arrow keys are pressed', async () => {
    const user = userEvent.setup()
    renderTabs(1)
    const tab = screen.getByRole('tab', { name: 'Tab 1' })
    tab.focus()

    await user.keyboard('{ArrowRight}')
    expect(tab).toHaveFocus()
    await user.keyboard('{ArrowLeft}')

    expect(tab).toHaveFocus()
    expect(tab).toHaveAttribute('aria-selected', 'true')
  })
})
