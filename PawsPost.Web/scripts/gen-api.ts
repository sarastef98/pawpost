import { mkdirSync, writeFileSync } from 'node:fs'
import openapiTS, { astToString } from 'openapi-typescript'
import { getApiUrl } from './api-url.ts'

// Generates src/api/schema.d.ts from the running API. Start the API first:
//   dotnet run --project ../PawsPost --launch-profile http
const specUrl = new URL('/openapi/v1.json', getApiUrl())
const outFile = new URL('../src/api/schema.d.ts', import.meta.url)

try {
  const ast = await openapiTS(specUrl)
  mkdirSync(new URL('.', outFile), { recursive: true })
  writeFileSync(outFile, astToString(ast))
  console.log(`Wrote src/api/schema.d.ts from ${specUrl}`)
} catch (error) {
  console.error(`Could not generate types from ${specUrl}. Is the API running?`)
  throw error
}
