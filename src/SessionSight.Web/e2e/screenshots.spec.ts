import { test } from '@playwright/test'
import { mockPatientSummary, mockPracticeSummary } from '../src/test/fixtures/summary'
import { mockReviewStats, mockReviewQueue, mockReviewDetail } from '../src/test/fixtures/review'
import { mockPatientRiskTrend } from '../src/test/fixtures/riskTrend'
import { mockPatients } from '../src/test/fixtures/patients'
import { mockQAResponse } from '../src/test/fixtures/qa'

function mockDashboardRoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/**', (route) => route.fulfill({ json: [] })),
    page.route('**/api/summary/practice**', (route) =>
      route.fulfill({ json: mockPracticeSummary }),
    ),
    page.route('**/api/review/stats', (route) =>
      route.fulfill({ json: mockReviewStats }),
    ),
    page.route('**/api/summary/patient/**/risk-trend**', (route) =>
      route.fulfill({ json: mockPatientRiskTrend }),
    ),
    page.route('**/api/review/queue**', (route) =>
      route.fulfill({ json: mockReviewQueue }),
    )
  ])
}

function mockSessionDetailRoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/**', (route) => route.fulfill({ json: [] })),
    page.route('**/api/review/session/**', (route) =>
      route.fulfill({ json: mockReviewDetail }),
    )
  ])
}

function mockQARoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/**', (route) => route.fulfill({ json: [] })),
    page.route('**/api/patients', (route) =>
      route.fulfill({ json: mockPatients }),
    ),
    page.route('**/api/qa/patient/**', async (route) => {
      await new Promise(r => setTimeout(r, 500))
      return route.fulfill({ json: mockQAResponse })
    })
  ])
}

test.describe('Generate Screenshots', () => {
  test.use({ viewport: { width: 1280, height: 800 } })

  test('capture dashboard screenshot', async ({ page }) => {
    await mockDashboardRoutes(page)
    await page.goto('/')
    await page.waitForTimeout(1000)
    await page.screenshot({ path: '../../docs/assets/dashboard.png', fullPage: true })
  })

  test('capture extraction trace screenshot', async ({ page }) => {
    await mockSessionDetailRoutes(page)
    await page.goto('/review/session/sess-001')
    await page.waitForTimeout(1000)
    await page.screenshot({ path: '../../docs/assets/extraction.png', fullPage: true })
  })

  test('capture rag qa screenshot', async ({ page }) => {
    await mockQARoutes(page)
    await page.goto('/qa')
    await page.waitForTimeout(2000)
    await page.screenshot({ path: '../../docs/assets/rag-qa.png', fullPage: true })
  })
})
