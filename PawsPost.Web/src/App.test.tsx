import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { client } from './api/client'

// Replace the real client so tests never make network calls.
vi.mock('./api/client', () => ({ client: { GET: vi.fn() } }))

const mockGet = vi.mocked(client.GET)

describe('App', () => {
  beforeEach(() => {
    mockGet.mockReset()
  })

  it('render_default_showsHeading', () => {
    // Arrange
    mockGet.mockResolvedValue({ response: { ok: true } } as never)

    // Act
    render(<App />)

    // Assert
    expect(
      screen.getByRole('heading', { name: 'Get started' }),
    ).toBeInTheDocument()
  })

  it('render_healthEndpointOk_showsApiOk', async () => {
    // Arrange
    mockGet.mockResolvedValue({ response: { ok: true } } as never)

    // Act
    render(<App />)

    // Assert
    expect(await screen.findByText('API: ok')).toBeInTheDocument()
    expect(mockGet).toHaveBeenCalledWith('/api/health')
  })

  it('render_healthEndpointFails_showsApiUnreachable', async () => {
    // Arrange
    mockGet.mockRejectedValue(new TypeError('Failed to fetch'))

    // Act
    render(<App />)

    // Assert
    expect(await screen.findByText('API: unreachable')).toBeInTheDocument()
  })

  it('render_healthEndpointReturnsError_showsApiUnreachable', async () => {
    // Arrange
    mockGet.mockResolvedValue({ response: { ok: false } } as never)

    // Act
    render(<App />)

    // Assert
    expect(await screen.findByText('API: unreachable')).toBeInTheDocument()
  })
})
