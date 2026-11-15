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
import PetAdDetailsPage from './PetAdDetailsPage'
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

describe('CreatePetAdPage age unit keyboard use', () => {
  it('moves the age unit from Months to Years with the right arrow key', async () => {
    renderPage()
    const user = userEvent.setup()
    screen.getByLabelText('Months').focus()

    await user.keyboard('{ArrowRight}')

    expect(screen.getByLabelText('Years')).toBeChecked()
    expect(screen.getByLabelText('Years')).toHaveFocus()
    expect(screen.getByLabelText('Months')).not.toBeChecked()
  })

  it('moves the age unit back to Months with the left arrow key', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Years'))

    await user.keyboard('{ArrowLeft}')

    expect(screen.getByLabelText('Months')).toBeChecked()
  })

  it('reaches the age unit group with the Tab key from the age field', async () => {
    renderPage()
    const user = userEvent.setup()
    await user.click(screen.getByLabelText('Age'))

    await user.tab()

    expect(screen.getByLabelText('Months')).toHaveFocus()
  })

  it('sends the unit chosen with the keyboard', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    const user = await fillValid({ age: '2' })
    screen.getByLabelText('Months').focus()
    await user.keyboard('{ArrowRight}')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody().ageInMonths).toBe(24)
  })
})

describe('CreatePetAdPage server errors per field', () => {
  it('shows a type only server error next to the type field and keeps other values', async () => {
    post.mockResolvedValue(
      failResult(400, { errors: { type: ['Unknown type.'] } }),
    )
    renderPage()
    await fillValid()

    await submit()

    expect(await screen.findByText('Unknown type.')).toBeVisible()
    expect(screen.getByLabelText('Type')).toHaveAccessibleDescription(
      'Unknown type.',
    )
    await waitFor(() => expect(screen.getByLabelText('Type')).toHaveFocus())
    expect(screen.getByLabelText('Title')).toHaveValue('Friendly Rex')
    expect(screen.getByLabelText('Title')).not.toBeInvalid()
    expect(screen.getByLabelText('Species')).not.toBeInvalid()
  })

  it('shows server errors for price, breed and location next to their fields', async () => {
    post.mockResolvedValue(
      failResult(400, {
        errors: {
          Price: ['Server price.'],
          '$.breed': ['Server breed.'],
          location: ['Server location.'],
        },
      }),
    )
    renderPage()
    const user = await fillValid({ type: 'Sale' })
    await user.type(screen.getByLabelText('Price'), '10')
    await user.type(screen.getByLabelText('Breed'), 'Dutch')
    await user.type(screen.getByLabelText('Location'), 'Skopje')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    expect(await screen.findByText('Server price.')).toBeVisible()
    expect(screen.getByLabelText('Price')).toHaveAccessibleDescription(
      'Server price.',
    )
    expect(screen.getByLabelText('Breed')).toHaveAccessibleDescription(
      'Server breed.',
    )
    expect(screen.getByLabelText('Location')).toHaveAccessibleDescription(
      'Server location.',
    )
    expect(screen.getByLabelText('Breed')).toHaveValue('Dutch')
  })

  it('focuses the first field with a server error in form order', async () => {
    post.mockResolvedValue(
      failResult(400, {
        errors: { location: ['Server location.'], breed: ['Server breed.'] },
      }),
    )
    renderPage()
    await fillValid()

    await submit()

    await screen.findByText('Server breed.')
    await waitFor(() => expect(screen.getByLabelText('Breed')).toHaveFocus())
  })
})

describe('CreatePetAdPage whitespace only optional fields', () => {
  it('sends no breed or location when they contain only spaces', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    const user = await fillValid()
    await user.type(screen.getByLabelText('Breed'), '   ')
    await user.type(screen.getByLabelText('Location'), '   ')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(sentBody()).not.toHaveProperty('breed')
    expect(sentBody()).not.toHaveProperty('location')
  })

  it('shows no error next to whitespace only breed and location', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-1' }, 201))
    renderPage()
    const user = await fillValid()
    await user.type(screen.getByLabelText('Breed'), '   ')
    await user.type(screen.getByLabelText('Location'), '   ')

    await user.click(screen.getByRole('button', { name: 'Post ad' }))

    await screen.findByText('Details probe')
    expect(post).toHaveBeenCalledTimes(1)
  })
})

describe('CreatePetAdPage success notice on the details page', () => {
  const get = client.GET as unknown as Mock

  function renderFlow(
    entries: Parameters<typeof MemoryRouter>[0]['initialEntries'],
  ) {
    const value = {
      status: 'loggedIn' as const,
      user: { id: 'u1', email: 'sara@example.com' },
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
    }
    render(
      <AuthContext.Provider value={value}>
        <MemoryRouter initialEntries={entries}>
          <Routes>
            <Route path="/pet-ads/new" element={<CreatePetAdPage />} />
            <Route path="/pet-ads/:id" element={<PetAdDetailsPage />} />
          </Routes>
        </MemoryRouter>
      </AuthContext.Provider>,
    )
  }

  const ad = {
    id: 'ad-9',
    title: 'Friendly Rex',
    type: 'Adoption',
    species: 'Rabbit',
    ageInMonths: 8,
    description: 'A calm pet.',
    status: 'Active',
    createdAt: '2026-11-01T10:00:00Z',
  }

  beforeEach(() => {
    get.mockReset()
    get.mockResolvedValue(okResult(ad))
  })

  it('shows the success notice on the new ad after posting', async () => {
    post.mockResolvedValue(okResult({ id: 'ad-9' }, 201))
    renderFlow(['/pet-ads/new'])
    await fillValid()

    await submit()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' }),
    ).toBeInTheDocument()
    // The notice is filled into the status region on the next tick.
    expect(await screen.findByText('Your ad is live.')).toBeVisible()
    expect(screen.getByRole('status')).toHaveTextContent('Your ad is live.')
  })

  it('shows no notice when the details page is opened directly', async () => {
    renderFlow(['/pet-ads/ad-9'])

    await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
    expect(screen.queryByText('Your ad is live.')).toBeNull()
  })

  it('shows no notice when the router state is lost on reload', async () => {
    renderFlow([{ pathname: '/pet-ads/ad-9', state: null }])

    await screen.findByRole('heading', { level: 1, name: 'Friendly Rex' })
    expect(screen.queryByText('Your ad is live.')).toBeNull()
  })
})
