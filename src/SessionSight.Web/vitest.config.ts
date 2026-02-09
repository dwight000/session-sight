import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

const coverageThreshold = Number(process.env.COVERAGE_THRESHOLD ?? '83')

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'happy-dom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    include: ['__tests__/**/*.test.{ts,tsx}'],
    exclude: ['e2e/**', 'node_modules/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      reportsDirectory: './coverage',
      exclude: [
        'node_modules/**',
        'e2e/**',
        '__tests__/**',
        'src/test/**',
        '**/*.d.ts',
        'vite.config.ts',
        'vitest.config.ts',
        'playwright.config.ts',
        'eslint.config.js',
        'tailwind.config.js',
      ],
      thresholds: {
        statements: coverageThreshold,
        branches: coverageThreshold,
        functions: coverageThreshold,
        lines: coverageThreshold,
      },
    },
  },
})
