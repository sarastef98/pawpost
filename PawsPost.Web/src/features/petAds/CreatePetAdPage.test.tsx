import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { client } from '../../api/client'
import { deferred, failResult, okResult } from '../../test/apiResponses'
import { AuthContext } from '../auth/useAuth'
import type { AuthStatus } from '../auth/useAuth'
import CreatePetAdPage from './CreatePetAdPage'
import { CREATE_FAILED_MESSAGE } from './petAdFormErrors'

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
      <MemoryRouter initialEntries={['/pet-ads/new']}>
        <Routes>
          <Route path="/login" element={<h1>Login probe</h1>} />
          <Route path="/pet-ads/new" element={<CreatePetAdPage />} />
          <Route path="/pet-ads/:id" element={<h1>Details probe</h1>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

async function fillValid(
  options: { type?: string; age?: string; unit?: string } = {},
) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Title'), 'Friendly Rex')
  await user.selectOptions(
    screen.getByLabelText('Type'),
    options.type ?? 'Adoption',
  )
  await user.selectOptions(screen.getByLabelText('Species'), 'Rabbit')
  await user.type(screen.getByLabelText('Age'), options.age ?? '8')
  if (options.unit) await user.click(screen.getByLabelText(options.unit))
  await user.type(screen.getByLabelText('Description'), 'A calm pet.')
  return user
}

function submit() {
  return userEvent.click(screen.getByRole('button', { name: 'Post ad' }))
}

function sentBody() {
  return post.mock.calls[0][1].body as Record<string, unknown>
}

beforeEach(() => {
  post.mockReset()
})

describe('CreatePetAdPage', () => {
  it('redirects a logged out visitor to the login page', () => {
    renderPage('loggedOut')

    expect(screen.getByText('Login probe')).toBeInTheDocument()
  })

  it('shows a spinner and no form while the auth check runs', () => {
    renderPage('loading')

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).toBeNull()
    expect(screen.queryByText('Login probe')).toBeNull()
  })

  it('shows all labelled fields with breed and location marked optional', () => {
    renderPage()

    for (const label of [
      'Title',
      'Type',
      'Species',
      'Breed',
      'Age',
      'Description',
      'Location',
    ]) {
      expect(screen.getByLabelText(label)).toBeInTheDocument()
    }
    expect(screen.getByLabelText('Breed')).toHaveAccessibleDescription(
      'Optional',
    )
    expect(screen.getByLabelText('Location')).toHaveAccessibleDescription(
      'Optional',
    )
    const group = screen.getByRole('group', { name: 'Age unit' })
    expect(group).toContainElement(screen.getByLabelText('Months'))
    expect(group).toContainElement(screen.getByLabelText('Years'))
    expect(screen.getByLabelText('Months')).toBeChecked()
  })

  it('offers exactly the empty choice and the five species', () => {
    renderPage()

    const options = screen
      .getAllByRole('option')
      .filter((o) => screen.getByLabelText('Species').contains(o))
      .map((o) => o.textContent)
    expect(options).toEqual([
      'Choose a species',
      'Dog',
      'Cat',
      'Bird',
      'Rabbit',
      'Other',
    ])
  })

  it('sends the exact species name', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    await fillValid()

    await submit()

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody().species).toBe('Rabbit')
  })

  it('sends months as typed', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    await fillValid()

    await submit()

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody().ageInMonths).toBe(8)
  })

  it('converts whole years to months', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    await fillValid({ age: '2', unit: 'Years' })

    await submit()

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody().ageInMonths).toBe(24)
  })

  it('converts decimal years to months', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    await fillValid({ age: '1.5', unit: 'Years' })

    await submit()

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody().ageInMonths).toBe(18)
  })

  it('keeps the typed age number when the unit changes', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Age'), '8')

    await user.click(screen.getByLabelText('Years'))

    expect(screen.getByLabelText('Age')).toHaveValue(8)
  })

  it('shows and requires the price for Sale', async () => {
    renderPage()
    const user = await fillValid({ type: 'Sale' })

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    expect(await screen.findByText('Please enter the price.')).toBeVisible()
    expect(post).not.toHaveBeenCalled()
  })

  it('hides the price for Adoption and sends none', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    await fillValid()

    expect(screen.queryByLabelText('Price')).toBeNull()
    await submit()

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody()).not.toHaveProperty('price')
  })

  it('keeps the typed price when switching Sale, Adoption, Sale', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.selectOptions(screen.getByLabelText('Type'), 'Sale')
    await user.type(screen.getByLabelText('Price'), '150')

    await user.selectOptions(screen.getByLabelText('Type'), 'Adoption')
    await user.selectOptions(screen.getByLabelText('Type'), 'Sale')

    expect(screen.getByLabelText('Price')).toHaveValue('150')
  })

  it('does not send an Adoption price that was typed before switching', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    const user = await fillValid({ type: 'Sale' })
    await user.type(screen.getByLabelText('Price'), '150')
    await user.selectOptions(screen.getByLabelText('Type'), 'Adoption')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody()).not.toHaveProperty('price')
  })

  it('shows required messages next to the fields and does not send', async () => {
    renderPage()

    await submit()

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription(
      'Please enter a title.',
    )
    expect(screen.getByLabelText('Type')).toHaveAccessibleDescription(
      'Please choose a type.',
    )
    expect(screen.getByLabelText('Species')).toHaveAccessibleDescription(
      'Please choose a species.',
    )
    expect(screen.getByLabelText('Age')).toHaveAccessibleDescription(
      'Please enter the age.',
    )
    expect(screen.getByLabelText('Description')).toHaveAccessibleDescription(
      'Please enter a description.',
    )
    expect(post).not.toHaveBeenCalled()
  })

  it('shows a too long message for the title', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Title'))
    await user.paste('a'.repeat(101))

    await submit()

    expect(
      screen.getByText('Title can be at most 100 characters.'),
    ).toBeVisible()
  })

  it('shows an age out of range message for months', async () => {
    renderPage()
    await fillValid({ age: '361' })

    await submit()

    expect(
      screen.getByText('Age can be at most 30 years (360 months).'),
    ).toBeVisible()
  })

  it('shows an age out of range message for years', async () => {
    renderPage()
    await fillValid({ age: '30.1', unit: 'Years' })

    await submit()

    expect(
      screen.getByText('Age can be at most 30 years (360 months).'),
    ).toBeVisible()
  })

  it('shows a message when the price is zero', async () => {
    renderPage()
    const user = await fillValid({ type: 'Sale' })
    await user.type(screen.getByLabelText('Price'), '0')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    expect(screen.getByText('Price must be greater than 0.')).toBeVisible()
  })

  it('moves focus to the first invalid field', async () => {
    renderPage()

    await submit()

    expect(screen.getByLabelText('Title')).toHaveFocus()
  })

  it('goes to the new ad and shows nothing extra in the body on success', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-9' }, 201))
    renderPage()
    await fillValid({ type: 'Sale' })
    await userEvent.type(screen.getByLabelText('Price'), '12.50')

    await submit()

    expect(await screen.findByText('Details probe')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/pet-ads', {
      body: {
        title: 'Friendly Rex',
        type: 'Sale',
        species: 'Rabbit',
        ageInMonths: 8,
        description: 'A calm pet.',
        price: 12.5,
      },
    })
  })

  it('shows server messages next to the matching fields, including $. and mixed-case keys', async () => {
    post.mockResolvedValue(
      failResult(400, {
        errors: {
          '$.title': ['Server title.'],
          Description: ['Server description.'],
          AgeInMonths: ['Server age.'],
        },
      }),
    )
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText('Server title.')).toBeVisible()
    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription(
      'Server title.',
    )
    expect(screen.getByLabelText('Description')).toHaveAccessibleDescription(
      'Server description.',
    )
    expect(screen.getByLabelText('Age')).toHaveAccessibleDescription(
      'Server age.',
    )
  })

  it('shows unknown server keys as a general message', async () => {
    post.mockResolvedValue(failResult(400, { errors: { Weird: ['Odd.'] } }))
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText('Odd.')).toBeVisible()
  })

  it('shows the species message, focuses species and keeps other values when only species is reported', async () => {
    post.mockResolvedValueOnce(
      failResult(400, { errors: { species: ['Unknown species.'] } }),
    )
    renderPage()
    const user = await fillValid()

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    expect(await screen.findByText('Unknown species.')).toBeVisible()
    await waitFor(() => expect(screen.getByLabelText('Species')).toHaveFocus())
    expect(screen.getByLabelText('Title')).toHaveValue('Friendly Rex')
    expect(screen.getByLabelText('Title')).not.toBeInvalid()
    expect(screen.getByLabelText('Description')).not.toBeInvalid()
    expect(screen.getByLabelText('Age')).not.toBeInvalid()
  })

  it('shows new field errors on a second submit after species is fixed', async () => {
    post
      .mockResolvedValueOnce(
        failResult(400, { errors: { species: ['Unknown species.'] } }),
      )
      .mockResolvedValueOnce(
        failResult(400, { errors: { Title: ['Title taken.'] } }),
      )
    renderPage()
    const user = await fillValid()
    await user.click(screen.getByRole('button', { name: 'Post ad' }))
    await screen.findByText('Unknown species.')

    await user.selectOptions(screen.getByLabelText('Species'), 'Cat')
    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    expect(await screen.findByText('Title taken.')).toBeVisible()
    expect(screen.queryByText('Unknown species.')).toBeNull()
  })

  it('shows a friendly message and keeps values after a server error', async () => {
    post.mockResolvedValue(
      failResult(500, { title: 'Internal Server Error', detail: 'stack' }),
    )
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText(CREATE_FAILED_MESSAGE)).toBeVisible()
    expect(screen.queryByText(/stack|500|Internal/)).toBeNull()
    expect(screen.getByLabelText('Title')).toHaveValue('Friendly Rex')
  })

  it('shows a friendly message and keeps values when the network fails', async () => {
    post.mockRejectedValue(new TypeError('Failed to fetch'))
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText(CREATE_FAILED_MESSAGE)).toBeVisible()
    expect(screen.queryByText(/Failed to fetch/)).toBeNull()
    expect(screen.getByLabelText('Title')).toHaveValue('Friendly Rex')
  })

  it('redirects to the login page on 401', async () => {
    post.mockResolvedValue(failResult(401, {}))
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText('Login probe')).toBeInTheDocument()
  })

  it('disables the button while saving and sends one request on a double click', async () => {
    const pending = deferred<ReturnType<typeof okResult>>()
    post.mockReturnValue(pending.promise)
    renderPage()
    const user = await fillValid()
    const button = screen.getByRole('button', { name: 'Post ad' })

    await user.dblClick(button)

    const busy = await screen.findByRole('button', { name: 'Please wait...' })
    expect(busy).toBeDisabled()
    expect(busy).toHaveAttribute('aria-busy', 'true')
    expect(post).toHaveBeenCalledTimes(1)
    pending.resolve(okResult({ id: 'ad-1' }, 201))
    expect(await screen.findByText('Details probe')).toBeInTheDocument()
  })
})
