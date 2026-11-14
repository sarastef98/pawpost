import { readFileSync } from 'node:fs'

const launchSettingsPath = new URL(
  '../../PawsPost/Properties/launchSettings.json',
  import.meta.url,
)

// Reads the API address from the "http" profile of the API's launchSettings.json,
// so the port is defined in one place only.
export function getApiUrl(): string {
  // The file starts with a byte order mark, which JSON.parse rejects.
  const text = readFileSync(launchSettingsPath, 'utf8').replace(/^\uFEFF/, '')
  const settings = JSON.parse(text) as {
    profiles?: { http?: { applicationUrl?: string } }
  }
  const url = settings.profiles?.http?.applicationUrl?.split(';')[0]
  if (!url) {
    throw new Error('No http applicationUrl found in launchSettings.json')
  }
  return url
}
