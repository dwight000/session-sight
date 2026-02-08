import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  webServer: {
    command: 'npm run dev',
    port: 5173,
    reuseExistingServer: true,
  },
  use: {
    baseURL: 'http://localhost:5173',
  },
  projects: [
    {
      name: 'chromium',
      testDir: './e2e',
      testIgnore: ['**/full-stack/**'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'fullStack',
      testDir: './e2e/full-stack',
      use: {
        ...devices['Desktop Chrome'],
        // Longer timeout for LLM extraction (2+ minutes)
        actionTimeout: 180_000,
      },
      // 5 min per test for full extraction pipeline
      timeout: 300_000,
    },
  ],
})
