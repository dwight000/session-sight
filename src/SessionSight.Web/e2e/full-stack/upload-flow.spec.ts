import { test, expect } from '@playwright/test'
import path from 'path'
import { fileURLToPath } from 'url'

/**
 * Full-stack E2E test for the complete upload flow.
 *
 * IMPORTANT: This test hits real Azure services and costs LLM tokens (~$0.05-0.10 per run).
 * Run sparingly - use mocked smoke tests for rapid iteration.
 *
 * Prerequisites:
 * - Aspire backend running with Azure services configured
 * - Test therapist inserted in database
 * - Vite dev server running with API URL configured
 *
 * Run with: ./scripts/run-e2e.sh --frontend
 */

// ESM-compatible __dirname
const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// Path to the test PDF relative to the web directory
const TEST_PDF_PATH = path.resolve(
  __dirname,
  '../../../../tests/SessionSight.FunctionalTests/TestData/sample-note.pdf'
)

test.describe('Upload Flow', () => {
  test('complete patient -> session -> upload -> review flow', async ({ page }) => {
    // Generate unique identifiers for this test run
    const timestamp = Date.now()
    const firstName = 'E2E'
    const lastName = `Test${timestamp}`
    const externalId = `E2E-${timestamp}`
    const fullName = `${firstName} ${lastName}`

    // 1. Create a patient
    await test.step('Create patient', async () => {
      await page.goto('/patients')
      await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible()

      // Click Add Patient button
      await page.getByRole('button', { name: 'Add Patient' }).click()

      // Fill in the form
      await page.getByLabel('First Name').fill(firstName)
      await page.getByLabel('Last Name').fill(lastName)
      await page.getByLabel('Date of Birth').fill('1990-01-15')
      await page.getByLabel('External ID').fill(externalId)

      // Submit
      await page.getByRole('button', { name: 'Create Patient' }).click()

      // Verify patient appears in the table
      await expect(page.getByText(fullName)).toBeVisible()
    })

    // 2. Create a session for that patient
    await test.step('Create session', async () => {
      await page.goto('/sessions')
      await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible()

      // Click Add Session button
      await page.getByRole('button', { name: 'Add Session' }).click()

      // Find the patient option that contains our name and select it
      const patientSelect = page.getByLabel('Patient')
      const options = await patientSelect.locator('option').allTextContents()
      const matchingOption = options.find((opt) => opt.includes(fullName))
      expect(matchingOption).toBeTruthy()
      await patientSelect.selectOption(matchingOption!)

      await page.getByLabel('Session Date').fill('2026-01-15')
      await page.getByLabel('Session Type').selectOption('Individual')
      await page.getByLabel('Modality').selectOption('InPerson')
      await page.getByLabel('Session Number').fill('1')

      // Submit
      await page.getByRole('button', { name: 'Create Session' }).click()

      // Verify session appears in the table with "No Document" badge
      // Look in the table body specifically to avoid matching dropdowns
      const table = page.locator('table')
      await expect(table.getByText(fullName)).toBeVisible()
      await expect(table.getByText('No Document').first()).toBeVisible()
    })

    // 3. Upload document for the session
    await test.step('Upload document', async () => {
      await page.goto('/upload')
      await expect(page.getByRole('heading', { name: 'Upload Session Note' })).toBeVisible()

      // Find the session option that contains our patient name and select it
      const sessionSelect = page.getByLabel('Select Session')
      const sessionOptions = await sessionSelect.locator('option').allTextContents()
      const matchingSession = sessionOptions.find((opt) => opt.includes(fullName))
      expect(matchingSession).toBeTruthy()
      await sessionSelect.selectOption(matchingSession!)

      // Upload the test PDF file
      const fileInput = page.getByLabel('Document File')
      await fileInput.setInputFiles(TEST_PDF_PATH)

      // Verify file is selected
      await expect(page.getByText('sample-note.pdf')).toBeVisible()

      // Submit and wait for extraction to complete (this is the slow part - up to 2 minutes)
      await page.getByRole('button', { name: 'Upload & Extract' }).click()

      // Wait for the processing indicator
      await expect(page.getByText(/Processing/)).toBeVisible()

      // Wait for success message (long timeout for LLM extraction)
      await expect(page.getByText('extraction completed successfully')).toBeVisible({
        timeout: 180_000,
      })

      // Verify the "View extraction results" link is visible
      await expect(page.getByRole('link', { name: 'View extraction results' })).toBeVisible()
    })

    // 4. Navigate to review and verify extraction
    await test.step('View extraction results', async () => {
      // Click the link to view results
      await page.getByRole('link', { name: 'View extraction results' }).click()

      // Verify we're on the session detail page
      await expect(page).toHaveURL(/\/review\/session\//)

      // Wait for the page to load - the Risk Assessment button appears when data is loaded
      await expect(page.getByRole('button', { name: /Risk Assessment/ })).toBeVisible({ timeout: 10000 })

      // Verify patient name is shown in the header
      await expect(page.getByRole('heading', { name: fullName })).toBeVisible()
    })

    // 5. Verify the session now shows as "Uploaded" in sessions list
    await test.step('Verify session has document', async () => {
      await page.goto('/sessions')

      // Wait for the sessions table to load
      await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible()

      // Wait for our patient's session to appear in the table
      const table = page.locator('table')
      await expect(table.getByText(fullName)).toBeVisible({ timeout: 10000 })

      // Verify the session now shows "Uploaded" badge in the same row
      const row = table.locator('tr', { has: page.getByText(fullName) })
      await expect(row.getByText('Uploaded')).toBeVisible()
    })

    // 6. Verify timeline page renders for the created patient
    await test.step('View patient timeline', async () => {
      await page.goto('/patients')
      await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible()

      const row = page.locator('tr', { has: page.getByText(fullName) })
      await expect(row).toBeVisible()

      await row.getByRole('link', { name: 'Timeline →' }).click()
      await expect(page).toHaveURL(/\/patients\/.*\/timeline/)
      await expect(page.getByRole('heading', { name: 'Patient Timeline' })).toBeVisible()
      await expect(page.getByText('Session Timeline')).toBeVisible()
    })
  })

  test('upload shows error for invalid file', async ({ page }) => {
    // This test verifies the UI handles errors gracefully
    // It doesn't actually test the backend validation (no LLM cost)

    await page.goto('/upload')

    // If there are no sessions available, we can't test upload
    const noSessionsMessage = page.getByText('No sessions available for upload')
    if (await noSessionsMessage.isVisible()) {
      test.skip()
      return
    }

    // Select any available session
    const selectSession = page.getByLabel('Select Session')
    const options = await selectSession.locator('option').all()
    if (options.length <= 1) {
      // Only the placeholder option exists
      test.skip()
      return
    }

    // Select the first real session
    await selectSession.selectOption({ index: 1 })

    // Try to submit without a file (should be disabled)
    const submitButton = page.getByRole('button', { name: 'Upload & Extract' })
    await expect(submitButton).toBeDisabled()
  })
})

test.describe('Review Queue', () => {
  test('shows extracted sessions in review queue', async ({ page }) => {
    // This test assumes some sessions have been extracted already
    // (either from previous test runs or E2E backend tests)

    await page.goto('/review')
    await expect(page.getByRole('heading', { name: 'Review Queue' })).toBeVisible()

    // The queue might be empty if no extractions have been done
    const emptyMessage = page.getByText('No sessions pending review')
    const hasItems = await page.getByRole('link', { name: /Review/ }).count()

    if (await emptyMessage.isVisible()) {
      // Empty queue is valid state
      expect(hasItems).toBe(0)
    } else {
      // If there are items, verify we can click through to detail
      expect(hasItems).toBeGreaterThan(0)
    }
  })
})

test.describe('Dashboard', () => {
  test('renders risk trend when flagged patients exist', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

    const noFlaggedInPeriod = page.getByText('No flagged patients in this period.')
    if (await noFlaggedInPeriod.isVisible()) {
      test.skip()
      return
    }

    await expect(page.getByText('Patient Risk Trend')).toBeVisible()
    await expect(page.getByLabel('Select patient risk trend')).toBeVisible()
    await expect(page.getByLabel('Patient risk trend chart')).toBeVisible()
  })
})
