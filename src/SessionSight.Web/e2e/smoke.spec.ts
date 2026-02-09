import { test, expect } from '@playwright/test'
import { mockPracticeSummary } from '../src/test/fixtures/summary'
import { mockReviewStats, mockReviewQueue, mockReviewDetail } from '../src/test/fixtures/review'
import { mockPatientRiskTrend } from '../src/test/fixtures/riskTrend'
import { mockPatients } from '../src/test/fixtures/patients'
import { mockPatientTimeline } from '../src/test/fixtures/timeline'

function mockDashboardRoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/summary/practice**', (route) =>
      route.fulfill({ json: mockPracticeSummary }),
    ),
    page.route('**/api/review/stats', (route) =>
      route.fulfill({ json: mockReviewStats }),
    ),
    page.route('**/api/summary/patient/**/risk-trend**', (route) =>
      route.fulfill({ json: mockPatientRiskTrend }),
    ),
  ])
}

function mockReviewQueueRoutes(page: import('@playwright/test').Page) {
  return page.route('**/api/review/queue**', (route) =>
    route.fulfill({ json: mockReviewQueue }),
  )
}

function mockSessionDetailRoutes(page: import('@playwright/test').Page) {
  return page.route('**/api/review/session/**', (route) =>
    route.fulfill({ json: mockReviewDetail }),
  )
}

function mockPatientTimelineRoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/patients', (route) =>
      route.fulfill({ json: mockPatients }),
    ),
    page.route('**/api/patients/p1', (route) =>
      route.fulfill({ json: mockPatients[0] }),
    ),
    page.route('**/api/summary/patient/**/timeline**', (route) =>
      route.fulfill({ json: mockPatientTimeline }),
    ),
  ])
}

test('Dashboard shows stats', async ({ page }) => {
  await mockDashboardRoutes(page)
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  await expect(page.getByText('87')).toBeVisible()
  await expect(page.getByText('24', { exact: true })).toBeVisible()
})

test('Review Queue shows patient names', async ({ page }) => {
  await mockReviewQueueRoutes(page)
  await page.goto('/review')

  await expect(page.getByRole('heading', { name: 'Review Queue' })).toBeVisible()
  await expect(page.getByText('Alice Johnson')).toBeVisible()
  await expect(page.getByText('Bob Smith')).toBeVisible()
})

test('Session Detail shows patient and risk section', async ({ page }) => {
  await mockSessionDetailRoutes(page)
  await page.goto('/review/session/sess-001')

  await expect(page.getByText('Alice Johnson')).toBeVisible()
  await expect(page.getByRole('button', { name: /Risk Assessment/ })).toBeVisible()
})

test('Session Detail approve action submits approved review payload', async ({ page }) => {
  let capturedBody: Record<string, unknown> | null = null
  await page.route('**/api/review/session/**', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      capturedBody = request.postDataJSON() as Record<string, unknown>
      await route.fulfill({ status: 200, contentType: 'application/json', body: 'null' })
      return
    }

    await route.fulfill({ json: mockReviewDetail })
  })

  await page.goto('/review/session/sess-001')

  await expect(page.getByText('Submit Review')).toBeVisible()
  await page.getByLabel('Reviewer Name').fill('Dr. Smoke')
  await page.getByRole('button', { name: 'Approve' }).click()

  await expect(page.getByText('Review submitted.')).toBeVisible()
  expect(capturedBody).toEqual({
    action: 'Approved',
    reviewerName: 'Dr. Smoke',
  })
})

test('Session Detail dismiss action submits dismissed review payload', async ({ page }) => {
  let capturedBody: Record<string, unknown> | null = null
  await page.route('**/api/review/session/**', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      capturedBody = request.postDataJSON() as Record<string, unknown>
      await route.fulfill({ status: 200, contentType: 'application/json', body: 'null' })
      return
    }

    await route.fulfill({ json: mockReviewDetail })
  })

  await page.goto('/review/session/sess-001')

  await expect(page.getByText('Submit Review')).toBeVisible()
  await page.getByLabel('Reviewer Name').fill('Dr. Smoke')
  await page.getByLabel('Notes (optional)').fill('False positive')
  await page.getByRole('button', { name: 'Dismiss' }).click()

  await expect(page.getByText('Review submitted.')).toBeVisible()
  expect(capturedBody).toEqual({
    action: 'Dismissed',
    reviewerName: 'Dr. Smoke',
    notes: 'False positive',
  })
})

test('Sidebar navigation works', async ({ page }) => {
  await mockDashboardRoutes(page)
  await mockReviewQueueRoutes(page)
  await mockSessionDetailRoutes(page)

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

  await page.getByRole('link', { name: 'Review Queue' }).click()
  await expect(page).toHaveURL(/\/review$/)
  await expect(page.getByRole('heading', { name: 'Review Queue' })).toBeVisible()

  // Navigate to a session detail via the Review button and back
  await page.getByRole('link', { name: 'Review →' }).first().click()
  await expect(page).toHaveURL(/\/review\/session\/sess-/)

  await page.getByRole('link', { name: /Back to Queue/ }).click()
  await expect(page).toHaveURL(/\/review$/)
})

test('Patients page navigates to patient timeline', async ({ page }) => {
  await mockPatientTimelineRoutes(page)

  await page.goto('/patients')
  await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible()

  await page.getByRole('link', { name: 'Timeline →' }).first().click()
  await expect(page).toHaveURL(/\/patients\/p1\/timeline/)
  await expect(page.getByRole('heading', { name: 'Patient Timeline' })).toBeVisible()
  await expect(page.getByText('Session Timeline')).toBeVisible()
})
