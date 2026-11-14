import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'
import { getApiUrl } from './scripts/api-url.ts'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // In development the browser talks to Vite, and Vite forwards /api to the
    // API. The browser sees one origin, so no CORS setup is needed.
    proxy: {
      '/api': getApiUrl(),
    },
  },
  test: {
    // Run tests in a simulated browser (jsdom) instead of plain Node.
    environment: 'jsdom',
    // Runs before every test file: adds the jest-dom matchers and cleanup.
    setupFiles: ['./src/test/setup.ts'],
  },
})
