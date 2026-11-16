import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useRef, useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import Dialog from './Dialog'

type HarnessProps = {
  onCancel?: () => void
  cancelDisabled?: boolean
  closeOnCancel?: boolean
}

// A small page with an "Open" button that mounts the dialog, like a parent.
function Harness({
  onCancel = () => {},
  cancelDisabled = false,
  closeOnCancel = true,
}: HarnessProps) {
  const [open, setOpen] = useState(false)
  const cancelRef = useRef<HTMLButtonElement>(null)
  return (
    <>
      <div>
        <button type="button" onClick={() => setOpen(true)}>
          Open
        </button>
      </div>
      {open && (
        <Dialog
          title="Close this ad?"
          description="This cannot be undone."
          onCancel={() => {
            onCancel()
            if (closeOnCancel) setOpen(false)
          }}
          cancelDisabled={cancelDisabled}
          initialFocusRef={cancelRef}
        >
          <button type="button">First</button>
          <button type="button" ref={cancelRef}>
            Cancel
          </button>
          <button type="button">Last</button>
        </Dialog>
      )}
    </>
  )
}

async function openDialog(props: HarnessProps = {}) {
  const user = userEvent.setup()
  render(<Harness {...props} />)
  await user.click(screen.getByRole('button', { name: 'Open' }))
  return user
}

describe('Dialog', () => {
  // Reset the scroll style even when a test fails before its own cleanup, so
  // the value never leaks into later tests. Unmount first: an open dialog
  // restores the old value when it unmounts.
  afterEach(() => {
    cleanup()
    document.body.style.overflow = ''
  })

  it('renders a modal dialog named by its title and described by its text', async () => {
    await openDialog()

    const dialog = screen.getByRole('dialog', { name: 'Close this ad?' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAccessibleDescription('This cannot be undone.')
    expect(
      screen.getByRole('heading', { level: 2, name: 'Close this ad?' }),
    ).toBeInTheDocument()
  })

  it('moves focus to the given initial element when it opens', async () => {
    await openDialog()

    expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus()
  })

  it('moves focus from the last button to the first on Tab', async () => {
    const user = await openDialog()
    screen.getByRole('button', { name: 'Last' }).focus()

    await user.tab()

    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
  })

  it('moves focus from the first button to the last on Shift+Tab', async () => {
    const user = await openDialog()
    screen.getByRole('button', { name: 'First' }).focus()

    await user.tab({ shift: true })

    expect(screen.getByRole('button', { name: 'Last' })).toHaveFocus()
  })

  it('calls onCancel on Escape', async () => {
    const onCancel = vi.fn()
    const user = await openDialog({ onCancel })

    await user.keyboard('{Escape}')

    expect(onCancel).toHaveBeenCalledTimes(1)
  })

  it('does nothing on Escape while cancelDisabled is true', async () => {
    const onCancel = vi.fn()
    const user = await openDialog({ onCancel, cancelDisabled: true })

    await user.keyboard('{Escape}')

    expect(onCancel).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('returns focus to the opening button when it closes', async () => {
    const user = await openDialog()

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.getByRole('button', { name: 'Open' })).toHaveFocus()
  })

  it('makes the page behind inert while open and restores it after closing', async () => {
    const user = await openDialog()

    // The inert attribute is set on the body child that holds the page.
    const pageRoot = document.body.firstElementChild as HTMLElement
    expect(pageRoot).toHaveAttribute('inert')

    await user.keyboard('{Escape}')

    expect(pageRoot).not.toHaveAttribute('inert')
  })

  it('stops the page from scrolling while open and restores it after closing', async () => {
    document.body.style.overflow = 'auto'
    const user = await openDialog()

    expect(document.body.style.overflow).toBe('hidden')

    await user.keyboard('{Escape}')

    expect(document.body.style.overflow).toBe('auto')
  })

  it('does not close when the backdrop is clicked', async () => {
    const onCancel = vi.fn()
    const user = await openDialog({ onCancel })
    const backdrop = screen.getByRole('dialog').parentElement as HTMLElement

    await user.click(backdrop)

    expect(onCancel).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
