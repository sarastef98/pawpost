import createClient from 'openapi-fetch'
import type { paths } from './schema'

// Typed client: paths, parameters and responses are checked against the API's
// OpenAPI document. Requests use relative URLs (/api/...), which the Vite dev
// server proxies to the API.
export const client = createClient<paths>()
