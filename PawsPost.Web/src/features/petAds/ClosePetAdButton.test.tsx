import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, noContentResult } from '../../test/apiResponses'
import ClosePetAdButton from './ClosePetAdButton'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const post = client.POST as unknown as Mock

const TITLE = 'Close this ad?'
const TEXT = 'Closing removes this ad for everyone. This cannot be undone.'
const FORBIDDEN_TEXT = 'You can only close your own ads.'
const GONE_TEXT = 'This ad is already closed or no longer exists.'
const ERROR_TEXT = 'We could not close this ad. Please try again.'

function renderButton(adTitle?: string) {
  const onClosed = vi.fn()
  const onGone = vi.fn()
  render(
    <MemoryRouter initialEntries={['/page']}>
      <Routes>
        <Route path="/login" element={<h1>Login probe</h1>} />
        <Route
          path="/page"
          element={
            <ClosePetAdButton
              adId="ad-1"
              adTitle={adTitle}
              onClosed={onClosed}
              onGone={onGone}
            />
          }
        />
      </Routes>
    </MemoryRouter>,
  )
  return { onClosed, onGone, user: userEvent.setup() }
}

// Opens the dialog and presses the confirm button in it.
async function confirmClose(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Close ad' }))
  const dialog = await screen.findByRole('dialog')
  await user.click(within(dialog).getByRole('button', { name: 'Close ad' }))
}

describe('ClosePetAdButton', () => {
  beforeEach(() => {
    post.mockReset()
  })

  it('shows a Close ad button and no dialog', () => {
    renderButton()

    expect(screen.getByRole('button', { name: 'Close ad' })).toBeEnabled()
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('opens the confirmation dialog with its text when clicked', async () => {
    const { user } = renderButton()

    await user.click(screen.getByRole('button', { name: 'Close ad' }))

    const dialog = await screen.findByRole('dialog', { name: TITLE })
    expect(dialog).toHaveAccessibleDescription(TEXT)
    expect(post).not.toHaveBeenCalled()
  })

  it('moves focus to Cancel when the dialog opens', async () => {
    const { user } = renderButton()

    await user.click(screen.getByRole('button', { name: 'Close ad' }))

    expect(await screen.findByRole('button', { name: 'Cancel' })).toHaveFocus()
  })

  it('closes the dialog on Cancel, sends no request and returns focus to Close ad', async () => {
    const { user } = renderButton()
    await user.click(screen.getByRole('button', { name: 'Close ad' }))

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(post).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Close ad' })).toHaveFocus()
  })

  it('closes the dialog on Escape, sends no request and returns focus to Close ad', async () => {
    const { user } = renderButton()
    await user.click(screen.getByRole('button', { name: 'Close ad' }))
    await screen.findByRole('dialog')

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(post).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Close ad' })).toHaveFocus()
  })

  it('sends one close request for the ad id when confirmed', async () => {
    post.mockResolvedValue(noContentResult())
    const { user } = renderButton()

    await confirmClose(user)

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/pet-ads/{id}/close', {
      params: { path: { id: 'ad-1' } },
    })
  })

  it('sends one request when the confirm button is double-clicked', async () => {
    const pending = deferred<ReturnType<typeof noContentResult>>()
    post.mockReturnValue(pending.promise)
    const { user } = renderButton()
    await user.click(screen.getByRole('button', { name: 'Close ad' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Close ad' })

    await user.dblClick(confirm)

    expect(post).toHaveBeenCalledTimes(1)
    await act(async () => pending.resolve(noContentResult()))
  })

  it('shows Closing... with aria-disabled while the request runs and ignores Escape', async () => {
    const pending = deferred<ReturnType<typeof noContentResult>>()
    post.mockReturnValue(pending.promise)
    const { user } = renderButton()

    await confirmClose(user)

    const closing = await screen.findByRole('button', { name: 'Closing...' })
    expect(closing).toHaveAttribute('aria-disabled', 'true')
    await user.keyboard('{Escape}')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    await act(async () => pending.resolve(noContentResult()))
  })

  it('calls onClosed after 204', async () => {
    post.mockResolvedValue(noContentResult())
    const { user, onClosed, onGone } = renderButton()

    await confirmClose(user)

    await vi.waitFor(() => expect(onClosed).toHaveBeenCalledTimes(1), {
      timeout: 5_000,
    })
    expect(onGone).not.toHaveBeenCalled()
  })

  it('sends the user to the login page on 401', async () => {
    post.mockResolvedValue(failResult(401))
    const { user } = renderButton()

    await confirmClose(user)

    expect(
      await screen.findByRole('heading', { name: 'Login probe' }),
    ).toBeInTheDocument()
  })

  it('shows the own-ads message on 403 without the confirm button, and OK closes without onGone', async () => {
    post.mockResolvedValue(failResult(403))
    const { user, onGone, onClosed } = renderButton()

    await confirmClose(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(FORBIDDEN_TEXT)
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getAllByRole('button')).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: 'OK' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(onGone).not.toHaveBeenCalled()
    expect(onClosed).not.toHaveBeenCalled()
  })

  it.each([404, 409, 400])(
    'shows the already closed message on %i and OK closes the dialog and calls onGone',
    async (code) => {
      post.mockResolvedValue(failResult(code))
      const { user, onGone } = renderButton()

      await confirmClose(user)

      expect(await screen.findByRole('alert')).toHaveTextContent(GONE_TEXT)
      await user.click(screen.getByRole('button', { name: 'OK' }))
      expect(screen.queryByRole('dialog')).toBeNull()
      expect(onGone).toHaveBeenCalledTimes(1)
    },
  )

  it('keeps focus inside the dialog on OK after a result', async () => {
    post.mockResolvedValue(failResult(404))
    const { user } = renderButton()

    await confirmClose(user)

    await screen.findByRole('alert')
    expect(screen.getByRole('button', { name: 'OK' })).toHaveFocus()
  })

  it('shows the try again message without technical details on a 500', async () => {
    post.mockResolvedValue(failResult(500, { title: 'Boom secret' }))
    const { user } = renderButton()

    await confirmClose(user)

    expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeEnabled()
    expect(screen.queryByText(/Boom secret/)).toBeNull()
  })

  it('shows the try again message without technical details on a network failure', async () => {
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    const { user } = renderButton()

    await confirmClose(user)

    expect(await screen.findByText(ERROR_TEXT)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeEnabled()
    expect(screen.queryByText(/Failed to fetch/)).toBeNull()
  })

  it('sends the request again on Try again and calls onClosed on 204', async () => {
    post.mockResolvedValueOnce(failResult(500))
    post.mockResolvedValueOnce(noContentResult())
    const { user, onClosed } = renderButton()
    await confirmClose(user)

    await user.click(await screen.findByRole('button', { name: 'Try again' }))

    await vi.waitFor(() => expect(onClosed).toHaveBeenCalledTimes(1), {
      timeout: 5_000,
    })
    expect(post).toHaveBeenCalledTimes(2)
  })

  it('uses the ad title in the accessible name when given', () => {
    renderButton('Friendly Rex')

    expect(
      screen.getByRole('button', { name: 'Close ad: Friendly Rex' }),
    ).toBeInTheDocument()
  })

  it('offers no reserve option in the button or the dialog', async () => {
    const { user } = renderButton()
    await user.click(screen.getByRole('button', { name: 'Close ad' }))
    await screen.findByRole('dialog')

    expect(screen.queryByRole('button', { name: /reserve/i })).toBeNull()
    expect(screen.queryByText(/reserve/i)).toBeNull()
  })
})
