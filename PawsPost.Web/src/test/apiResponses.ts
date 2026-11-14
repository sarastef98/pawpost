// Helpers that build the results the typed API client returns, so tests can
// answer at the client boundary without repeating the shape.

export function okResult(data: unknown, status = 200) {
  return { data, error: undefined, response: { status, ok: true } }
}

export function noContentResult() {
  return {
    data: undefined,
    error: undefined,
    response: { status: 204, ok: true },
  }
}

export function failResult(status: number, error: unknown = {}) {
  return { data: undefined, error, response: { status, ok: false } }
}

export function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => {
    resolve = r
  })
  return { promise, resolve }
}

export const testUser = { id: 'user-1', email: 'sara@example.com' }
