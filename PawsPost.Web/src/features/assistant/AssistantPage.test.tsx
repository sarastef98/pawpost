import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import {
  assistantBody,
  deferred,
  failResult,
  okResult,
  sampleAd,
} from '../../test/apiResponses'
import { AuthContext } from '../auth/useAuth'
import type { AuthStatus } from '../auth/useAuth'
import AssistantPage from './AssistantPage'
import { BAD_QUESTION_MESSAGE, SEARCH_FAILED_MESSAGE } from './assistantErrors'
import {
  EMPTY_QUESTION_MESSAGE,
  TOO_LONG_QUESTION_MESSAGE,
} from './assistantQuestion'

vi.mock('../../api/client', () => ({
  client: { GET: vi.fn(), POST: vi.fn() },
}))

const post = client.POST as unknown as Mock

function renderPage(status: AuthStatus = 'loggedIn') {
  const value = {
    status,
    user:
      status === 'loggedIn' ? { id: 'u1', email: 'sara@example.com' } : null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  }
  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/assistant']}>
        <Routes>
          <Route path="/login" element={<h1>Login probe</h1>} />
          <Route path="/assistant" element={<AssistantPage />} />
          <Route path="/pet-ads/:id" element={<h1>Details probe</h1>} />
          <Route path="/" element={<h1>Home probe</h1>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

function field() {
  return screen.getByLabelText('Your question')
}

async function ask(text = 'a calm dog') {
  const user = userEvent.setup()
  await user.type(field(), text)
  await user.click(screen.getByRole('button', { name: 'Ask' }))
  return user
}

const twoAds = [
  sampleAd({ id: 'ad-1', title: 'Friendly Rex' }),
  sampleAd({ id: 'ad-2', title: 'Misty the cat', species: 'Cat' }),
]

beforeEach(() => {
  post.mockReset()
})

describe('AssistantPage', () => {
  it('shows the answer and the matching ads after asking a question', async () => {
    post.mockResolvedValue(okResult(assistantBody('Rex is calm.', twoAds)))
    renderPage()

    await ask()

    expect(await screen.findByText('Rex is calm.')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 2, name: 'Matching ads' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Friendly Rex')).toBeInTheDocument()
    expect(screen.getByText('Misty the cat')).toBeInTheDocument()
  })

  it('shows the heading, intro and form before the first question without an empty state', () => {
    renderPage()

    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Find a pet with the assistant',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Describe the pet you are looking for in your own words.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('Up to 500 characters.')).toBeInTheDocument()
    expect(screen.queryByText(/No ads match/)).toBeNull()
    expect(post).not.toHaveBeenCalled()
  })

  it('sends the trimmed question to the assistant endpoint through the client', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', [])))
    renderPage()

    await ask('   a calm dog for children  ')

    await screen.findByText('Ok.')
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/assistant/search', {
      body: { question: 'a calm dog for children' },
    })
  })

  it('links each ad card to its details page', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', twoAds)))
    renderPage()

    await ask()

    expect(
      await screen.findByRole('link', { name: /Friendly Rex/ }),
    ).toHaveAttribute('href', '/pet-ads/ad-1')
    expect(screen.getByRole('link', { name: /Misty the cat/ })).toHaveAttribute(
      'href',
      '/pet-ads/ad-2',
    )
  })

  it('opens the details page when an ad card is clicked', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', twoAds)))
    renderPage()
    const user = await ask()

    await user.click(await screen.findByRole('link', { name: /Misty/ }))

    expect(screen.getByText('Details probe')).toBeInTheDocument()
  })

  it('shows a spinner and disables the Ask button while waiting for the answer', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderPage()

    await ask()

    expect(screen.getByText('Loading')).toBeInTheDocument()
    const button = screen.getByRole('button', { name: 'Please wait...' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(field()).toHaveAttribute('readonly')
    pending.resolve(okResult(assistantBody('Done.', [])))
    expect(await screen.findByText('Done.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ask' })).toBeEnabled()
  })

  it('sends only one request on a double click', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderPage()
    const user = userEvent.setup()
    await user.type(field(), 'a calm dog')

    await user.dblClick(screen.getByRole('button', { name: 'Ask' }))
    pending.resolve(okResult(assistantBody('Done.', [])))

    await screen.findByText('Done.')
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('sends only one request on a double Enter', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderPage()
    const user = userEvent.setup()

    await user.type(field(), 'a calm dog{Enter}{Enter}')
    pending.resolve(okResult(assistantBody('Done.', [])))

    await screen.findByText('Done.')
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('shows the answer and the empty state with a link to all ads when the server returns no ads', async () => {
    post.mockResolvedValue(
      okResult(assistantBody('Sorry, I could not find any ads.', [])),
    )
    renderPage()

    await ask()

    expect(
      await screen.findByText('Sorry, I could not find any ads.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'No ads match your question yet. Try describing the pet in other words.',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Browse all ads' }),
    ).toHaveAttribute('href', '/')
    expect(screen.queryByRole('list')).toBeNull()
  })

  it('shows only the ads from the response as links, not ads named in the answer', async () => {
    post.mockResolvedValue(
      okResult(
        assistantBody('I also like Ghost Cat, but take a look at Rex.', [
          sampleAd({ id: 'ad-1', title: 'Friendly Rex' }),
        ]),
      ),
    )
    renderPage()

    await ask()

    await screen.findByText(/Ghost Cat/)
    const list = screen.getByRole('list')
    expect(within(list).getAllByRole('link')).toHaveLength(1)
    expect(screen.queryByRole('link', { name: /Ghost Cat/ })).toBeNull()
  })

  it.each(['', '    '])(
    'does not send the question %j and shows a message next to the field',
    async (text) => {
      renderPage()
      const user = userEvent.setup()
      if (text) await user.type(field(), text)

      await user.click(screen.getByRole('button', { name: 'Ask' }))

      expect(post).not.toHaveBeenCalled()
      expect(field()).toHaveAccessibleDescription(
        expect.stringContaining(EMPTY_QUESTION_MESSAGE),
      )
      expect(field()).toBeInvalid()
    },
  )

  it('does not send a question longer than 500 characters after trimming', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(field())
    await user.paste('a'.repeat(501))

    await user.click(screen.getByRole('button', { name: 'Ask' }))

    expect(post).not.toHaveBeenCalled()
    expect(field()).toHaveAccessibleDescription(
      expect.stringContaining(TOO_LONG_QUESTION_MESSAGE),
    )
  })

  it('sends a question of exactly 500 characters with surrounding spaces', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', [])))
    renderPage()
    const user = userEvent.setup()
    await user.click(field())
    await user.paste(`  ${'a'.repeat(500)}  `)

    await user.click(screen.getByRole('button', { name: 'Ask' }))

    await screen.findByText('Ok.')
    expect(post.mock.calls[0][1].body.question).toBe('a'.repeat(500))
  })

  it.each(['question', 'Question', '$.question'])(
    'shows the 400 message under the key %s next to the field and moves focus to it',
    async (key) => {
      post.mockResolvedValue(
        failResult(400, { errors: { [key]: ['Server says no.'] } }),
      )
      renderPage()

      await ask()

      expect(await screen.findByText('Server says no.')).toBeInTheDocument()
      expect(field()).toHaveAccessibleDescription(
        expect.stringContaining('Server says no.'),
      )
      expect(field()).toHaveFocus()
    },
  )

  it('shows the fallback message when a 400 has no usable message', async () => {
    post.mockResolvedValue(failResult(400, {}))
    renderPage()

    await ask()

    expect(await screen.findByText(BAD_QUESTION_MESSAGE)).toBeInTheDocument()
    expect(field()).toHaveAccessibleDescription(
      expect.stringContaining(BAD_QUESTION_MESSAGE),
    )
  })

  it('keeps the typed question in the field after a 400', async () => {
    post.mockResolvedValue(failResult(400, {}))
    renderPage()

    await ask('a calm dog')

    await screen.findByText(BAD_QUESTION_MESSAGE)
    expect(field()).toHaveValue('a calm dog')
  })

  it('redirects a logged out visitor to the login page without calling the client', () => {
    renderPage('loggedOut')

    expect(screen.getByText('Login probe')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('shows a spinner and no form, login page or request while the login state is loading', () => {
    renderPage('loading')

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByLabelText('Your question')).toBeNull()
    expect(screen.queryByText('Login probe')).toBeNull()
    expect(post).not.toHaveBeenCalled()
  })

  it('redirects to the login page when the API answers 401', async () => {
    post.mockResolvedValue(failResult(401))
    renderPage()

    await ask()

    expect(await screen.findByText('Login probe')).toBeInTheDocument()
  })

  it('shows an error message with Try again and no technical details on 503', async () => {
    post.mockResolvedValue(
      failResult(503, {
        title: 'The assistant is temporarily unavailable.',
        status: 503,
      }),
    )
    renderPage()

    await ask()

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(SEARCH_FAILED_MESSAGE)
    expect(
      within(alert).getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
    expect(alert).not.toHaveTextContent('503')
    expect(alert).not.toHaveTextContent('temporarily unavailable')
  })

  it('shows an error message when the API is unreachable', async () => {
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    renderPage()

    await ask()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      SEARCH_FAILED_MESSAGE,
    )
    expect(screen.queryByText(/Failed to fetch/)).toBeNull()
  })

  it('shows an error message when the response body has the wrong shape', async () => {
    post.mockResolvedValue(okResult({ answer: 42, ads: 'none' }))
    renderPage()

    await ask()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      SEARCH_FAILED_MESSAGE,
    )
  })

  it('re-sends the last submitted question when Try again is pressed and shows the answer', async () => {
    post.mockResolvedValueOnce(failResult(500))
    post.mockResolvedValueOnce(okResult(assistantBody('Second time.', [])))
    renderPage()
    const user = await ask('first question')
    await screen.findByRole('alert')
    await user.clear(field())
    await user.type(field(), 'something else')

    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Second time.')).toBeInTheDocument()
    expect(post).toHaveBeenCalledTimes(2)
    expect(post.mock.calls[1][1]).toEqual({
      body: { question: 'first question' },
    })
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('replaces the previous answer and ads when a new question is asked', async () => {
    post.mockResolvedValueOnce(
      okResult(
        assistantBody('First answer.', [
          sampleAd({ id: 'ad-1', title: 'Old ad' }),
        ]),
      ),
    )
    post.mockResolvedValueOnce(
      okResult(
        assistantBody('Second answer.', [
          sampleAd({ id: 'ad-9', title: 'New ad' }),
        ]),
      ),
    )
    renderPage()
    const user = await ask('first')
    await screen.findByText('First answer.')

    await user.clear(field())
    await user.type(field(), 'second')
    await user.click(screen.getByRole('button', { name: 'Ask' }))

    expect(await screen.findByText('Second answer.')).toBeInTheDocument()
    expect(screen.queryByText('First answer.')).toBeNull()
    expect(screen.queryByText('Old ad')).toBeNull()
    expect(screen.getByText('New ad')).toBeInTheDocument()
  })

  it('keeps the typed question in the field after the answer', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', [])))
    renderPage()

    await ask('a calm dog')

    await screen.findByText('Ok.')
    expect(field()).toHaveValue('a calm dog')
  })

  it('shows an answer containing HTML characters as visible text and creates no elements', async () => {
    const html = '<b>hi</b> <img src=x onerror=alert(1)>'
    post.mockResolvedValue(okResult(assistantBody(html, [])))
    renderPage()

    await ask()

    const answer = await screen.findByText(html)
    expect(answer.querySelector('b')).toBeNull()
    expect(answer.querySelector('img')).toBeNull()
    expect(screen.queryByRole('img')).toBeNull()
  })

  it('has a visible label for the question field', () => {
    renderPage()

    expect(screen.getByLabelText('Your question')).toBeVisible()
    expect(screen.getByText('Your question')).toBeVisible()
  })

  it('can be reached and submitted with the keyboard only', async () => {
    post.mockResolvedValue(okResult(assistantBody('By keyboard.', [])))
    renderPage()
    const user = userEvent.setup()

    await user.tab()
    expect(field()).toHaveFocus()
    await user.keyboard('a calm dog{Enter}')

    expect(await screen.findByText('By keyboard.')).toBeInTheDocument()
    expect(post).toHaveBeenCalledTimes(1)
  })

  it('announces the result in the status region', async () => {
    post.mockResolvedValue(okResult(assistantBody('Ok.', twoAds)))
    renderPage()

    await ask()

    await screen.findByText('Ok.')
    expect(screen.getByRole('status')).toHaveTextContent(
      'Answer ready. 2 ads found.',
    )
  })

  it('announces when no ads were found', async () => {
    post.mockResolvedValue(okResult(assistantBody('Sorry.', [])))
    renderPage()

    await ask()

    await screen.findByText('Sorry.')
    expect(screen.getByRole('status')).toHaveTextContent(
      'Answer ready. No ads found.',
    )
  })

  it('moves focus to the answer heading after Try again succeeds', async () => {
    post.mockResolvedValueOnce(failResult(500))
    post.mockResolvedValueOnce(okResult(assistantBody('Back.', [])))
    renderPage()
    const user = await ask()
    await screen.findByRole('alert')

    await user.click(screen.getByRole('button', { name: 'Try again' }))

    await screen.findByText('Back.')
    expect(screen.getByRole('heading', { name: 'Answer' })).toHaveFocus()
  })
})
