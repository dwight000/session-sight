import { test, expect } from '@playwright/test'
import { mockPracticeSummary } from '../src/test/fixtures/summary'
import { mockReviewStats, mockReviewQueue, mockReviewDetail } from '../src/test/fixtures/review'
import { mockPatientRiskTrend } from '../src/test/fixtures/riskTrend'
import { mockPatients } from '../src/test/fixtures/patients'
import { mockPatientTimeline } from '../src/test/fixtures/timeline'
import { mockTherapists } from '../src/test/fixtures/therapists'
import { mockProcessingJobs } from '../src/test/fixtures/processingJobs'
import { mockSessions } from '../src/test/fixtures/sessions'
import { mockQAResponse } from '../src/test/fixtures/qa'

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

test('Therapists page shows therapist table', async ({ page }) => {
  await page.route('**/api/therapists', (route) =>
    route.fulfill({ json: mockTherapists }),
  )

  await page.goto('/therapists')

  await expect(page.getByRole('heading', { name: 'Therapists' })).toBeVisible()
  await expect(page.getByText('Default Therapist')).toBeVisible()
  await expect(page.getByText('Dr. Jane Wilson')).toBeVisible()
})

test('Therapists page create form captures correct payload', async ({ page }) => {
  let capturedBody: Record<string, unknown> | null = null
  await page.route('**/api/therapists', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      capturedBody = request.postDataJSON() as Record<string, unknown>
      await route.fulfill({ json: { id: 'new-t', ...capturedBody, createdAt: '2025-01-01T00:00:00Z', updatedAt: null } })
      return
    }
    await route.fulfill({ json: mockTherapists })
  })

  await page.goto('/therapists')
  await expect(page.getByText('Default Therapist')).toBeVisible()

  await page.getByRole('button', { name: 'Add Therapist' }).click()
  await page.getByLabel('Name').fill('Dr. Smoke Test')
  await page.getByLabel('License Number').fill('LIC-SMOKE')
  await page.getByLabel('Credentials').fill('PhD')
  await page.getByRole('button', { name: 'Create Therapist' }).click()

  expect(capturedBody).toEqual({
    name: 'Dr. Smoke Test',
    licenseNumber: 'LIC-SMOKE',
    credentials: 'PhD',
    isActive: true,
  })
})

test('Processing Jobs page shows job table', async ({ page }) => {
  await page.route('**/api/processing-jobs', (route) =>
    route.fulfill({ json: mockProcessingJobs }),
  )

  await page.goto('/jobs')

  await expect(page.getByRole('heading', { name: 'Processing Jobs' })).toBeVisible()
  await expect(page.getByText('extraction-session-001')).toBeVisible()
  // Use role-based selectors to avoid ambiguity between header and badge text
  const table = page.locator('table')
  await expect(table.getByRole('cell', { name: 'Completed' })).toBeVisible()
  await expect(table.getByRole('cell', { name: 'Processing' })).toBeVisible()
  await expect(table.getByRole('cell', { name: 'Failed' })).toBeVisible()
})

const mockSamplesJson = [
  {
    id: 'sample-nonrisk-001',
    filename: 'sample-nonrisk-001.pdf',
    title: 'Anxiety / CBT Session',
    description: 'GAD with cognitive restructuring, individual session',
    previewText: 'Session Note - March 5, 2026 Patient: Sarah Chen...',
  },
  {
    id: 'sample-risk-001',
    filename: 'sample-risk-001.pdf',
    title: 'Active SI with Safety Plan',
    description: 'High risk - specific plan, stockpiled means, emergency contacts',
    previewText: 'Session Note - March 20, 2026 Patient: Rachel Morrison...',
  },
]

function mockUploadRoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/sessions?*', (route) =>
      route.fulfill({ json: mockSessions }),
    ),
    page.route('**/api/sessions', (route) => {
      if (route.request().url().includes('?')) {
        return route.fulfill({ json: mockSessions })
      }
      return route.fulfill({ json: mockSessions })
    }),
    page.route('**/api/patients', (route) =>
      route.fulfill({ json: mockPatients }),
    ),
    page.route('**/samples/samples.json', (route) =>
      route.fulfill({ json: mockSamplesJson }),
    ),
    page.route('**/samples/*.pdf', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: Buffer.from('%PDF-1.4 fake'),
      }),
    ),
  ])
}

test('Upload page shows sample document cards by default', async ({ page }) => {
  await mockUploadRoutes(page)
  await page.goto('/upload')

  await expect(page.getByRole('heading', { name: 'Upload Session Note' })).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Sample Documents' })).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Your Document' })).toBeVisible()

  // Sample cards should be visible by default
  await expect(page.getByText('Anxiety / CBT Session')).toBeVisible()
  await expect(page.getByText('Active SI with Safety Plan')).toBeVisible()
})

test('Upload page sample card preview toggles', async ({ page }) => {
  await mockUploadRoutes(page)
  await page.goto('/upload')

  await expect(page.getByText('Anxiety / CBT Session')).toBeVisible()

  // Click Preview on first card
  await page.getByText('Preview').first().click()
  await expect(page.getByText('Session Note - March 5, 2026')).toBeVisible()

  // Click Hide to collapse
  await page.getByText('Hide').first().click()
  await expect(page.getByText('Session Note - March 5, 2026')).not.toBeVisible()
})

test('Upload page Use This sets selected file', async ({ page }) => {
  await mockUploadRoutes(page)
  await page.goto('/upload')

  await expect(page.getByText('Anxiety / CBT Session')).toBeVisible()

  // Click Use This on first card
  await page.getByText('Use This').first().click()

  // Should show selected file info
  await expect(page.getByText(/Selected file:.*sample-nonrisk-001\.pdf/)).toBeVisible()
})

test('Upload page Your Document tab shows file input', async ({ page }) => {
  await mockUploadRoutes(page)
  await page.goto('/upload')

  await expect(page.getByRole('heading', { name: 'Upload Session Note' })).toBeVisible()

  // File input should NOT be visible on Sample Documents tab
  await expect(page.getByLabel('Document File')).not.toBeVisible()

  // Switch to Your Document tab
  await page.getByRole('tab', { name: 'Your Document' }).click()

  // File input should now be visible
  await expect(page.getByLabel('Document File')).toBeVisible()
  await expect(page.getByText('Supported formats: PDF, DOCX, DOC, JPG, PNG, TIFF, BMP')).toBeVisible()
})

test('Sessions form includes therapist dropdown', async ({ page }) => {
  await Promise.all([
    page.route('**/api/sessions', (route) =>
      route.fulfill({ json: [] }),
    ),
    page.route('**/api/sessions?*', (route) =>
      route.fulfill({ json: [] }),
    ),
    page.route('**/api/patients', (route) =>
      route.fulfill({ json: mockPatients }),
    ),
    page.route('**/api/therapists', (route) =>
      route.fulfill({ json: mockTherapists }),
    ),
  ])

  await page.goto('/sessions')
  await expect(page.getByRole('heading', { name: 'Sessions', exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Add Session' }).click()
  await expect(page.getByLabel('Therapist')).toBeVisible()

  // Verify therapist options are populated
  const therapistSelect = page.getByLabel('Therapist')
  const options = await therapistSelect.locator('option').allTextContents()
  expect(options).toContain('Default Therapist')
  expect(options).toContain('Dr. Jane Wilson')
})

function mockQARoutes(page: import('@playwright/test').Page) {
  return Promise.all([
    page.route('**/api/patients', (route) =>
      route.fulfill({ json: mockPatients }),
    ),
    page.route('**/api/qa/patient/**', (route) =>
      route.fulfill({ json: mockQAResponse }),
    ),
  ])
}

test('Q&A page shows heading and patient selector', async ({ page }) => {
  await mockQARoutes(page)
  await page.goto('/qa')

  await expect(page.getByRole('heading', { name: 'Q&A' })).toBeVisible()
  await expect(page.getByLabel('Patient')).toBeVisible()
  await expect(page.getByLabel('Question')).toBeVisible()
})

test('Q&A page submits question and shows answer', async ({ page }) => {
  let capturedBody: Record<string, unknown> | null = null
  await page.route('**/api/patients', (route) =>
    route.fulfill({ json: mockPatients }),
  )
  await page.route('**/api/qa/patient/**', async (route) => {
    capturedBody = route.request().postDataJSON() as Record<string, unknown>
    await route.fulfill({ json: mockQAResponse })
  })

  await page.goto('/qa')

  await page.getByLabel('Patient').selectOption('p1')
  await page.getByLabel('Question').fill('What concerns were discussed?')
  await page.getByRole('button', { name: 'Ask' }).click()

  await expect(page.getByText('The patient discussed anxiety')).toBeVisible()
  await expect(page.getByText('View session')).toBeVisible()
  expect(capturedBody).toEqual({ question: 'What concerns were discussed?' })
})
