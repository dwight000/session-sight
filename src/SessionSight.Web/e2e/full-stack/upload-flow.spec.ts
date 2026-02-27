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

      // Select E2E therapist (created by run-e2e.sh setup)
      const therapistSelect = page.getByLabel('Therapist')
      await therapistSelect.selectOption({ index: 1 })

      await page.getByLabel('Session Date').fill('2026-01-15')
      await page.getByLabel('Session Type').selectOption('Individual')
      await page.getByLabel('Modality').selectOption('InPerson')
      await page.getByLabel('Session Number').fill('1')

      // Submit
      await page.getByRole('button', { name: 'Create Session' }).click()

      // Verify session appears in the table with "No Document" badge
      // Look in the table body specifically to avoid matching dropdowns
      const table = page.locator('table')
      await expect(table.getByText(fullName)).toBeVisible({ timeout: 6000 })
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

      // Switch to "Your Document" tab to access file input
      await page.getByRole('tab', { name: 'Your Document' }).click()

      // Upload the test PDF file
      const fileInput = page.getByLabel('Document File')
      await fileInput.setInputFiles(TEST_PDF_PATH)

      // Verify file is selected
      await expect(page.getByText('sample-note.pdf')).toBeVisible()

      // Submit and wait for extraction to complete (this is the slow part - up to 2 minutes)
      await page.getByRole('button', { name: 'Upload & Extract' }).click()

      // Verify pipeline UI shows step names during extraction
      await expect(page.getByText('Extraction Pipeline')).toBeVisible({ timeout: 10_000 })
      await expect(page.getByRole('button', { name: /Document Parse/ })).toBeVisible()
      await expect(page.getByRole('button', { name: /Intake/ })).toBeVisible()
      await expect(page.getByRole('button', { name: /Clinical Extract/ })).toBeVisible()

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

      // Verify Processing Log section renders with completed steps
      await expect(page.getByRole('heading', { name: 'Processing Log' })).toBeVisible({ timeout: 10_000 })
      await expect(page.getByRole('button', { name: /Document Parse/ })).toBeVisible()
      await expect(page.getByRole('button', { name: /Search Index/ })).toBeVisible()
    })

    // 5. Verify the session now shows as "Extracted" in sessions list
    await test.step('Verify session has document', async () => {
      await page.goto('/sessions')

      // Wait for the sessions table to load
      await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible()

      // Wait for our patient's session to appear in the table
      const table = page.locator('table')
      await expect(table.getByText(fullName)).toBeVisible({ timeout: 10000 })

      // Verify the session now shows "Extracted" badge (extraction completed)
      const row = table.locator('tr', { has: page.getByText(fullName) })
      await expect(row.getByText('Extracted')).toBeVisible()
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

  test('upload button disabled without file selected', async ({ page }) => {
    // Create own patient + session so this test never depends on leftover data
    const timestamp = Date.now()
    const firstName = 'E2E'
    const lastName = `NoFile${timestamp}`
    const fullName = `${firstName} ${lastName}`

    // Create patient
    await page.goto('/patients')
    await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible()
    await page.getByRole('button', { name: 'Add Patient' }).click()
    await page.getByLabel('First Name').fill(firstName)
    await page.getByLabel('Last Name').fill(lastName)
    await page.getByLabel('Date of Birth').fill('1990-01-15')
    await page.getByLabel('External ID').fill(`E2E-NF-${timestamp}`)
    await page.getByRole('button', { name: 'Create Patient' }).click()
    await expect(page.getByText(fullName)).toBeVisible()

    // Create session
    await page.goto('/sessions')
    await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible()
    await page.getByRole('button', { name: 'Add Session' }).click()

    const patientSelect = page.getByLabel('Patient')
    const options = await patientSelect.locator('option').allTextContents()
    const matchingOption = options.find((opt) => opt.includes(fullName))
    expect(matchingOption).toBeTruthy()
    await patientSelect.selectOption(matchingOption!)

    const therapistSelect = page.getByLabel('Therapist')
    await therapistSelect.selectOption({ index: 1 })

    await page.getByLabel('Session Date').fill('2026-01-15')
    await page.getByLabel('Session Type').selectOption('Individual')
    await page.getByLabel('Modality').selectOption('InPerson')
    await page.getByLabel('Session Number').fill('1')
    await page.getByRole('button', { name: 'Create Session' }).click()

    const table = page.locator('table')
    await expect(table.getByText(fullName)).toBeVisible({ timeout: 6000 })

    // Navigate to upload and select our session
    await page.goto('/upload')
    await expect(page.getByRole('heading', { name: 'Upload Session Note' })).toBeVisible()

    const sessionSelect = page.getByLabel('Select Session')
    const sessionOptions = await sessionSelect.locator('option').allTextContents()
    const matchingSession = sessionOptions.find((opt) => opt.includes(fullName))
    expect(matchingSession).toBeTruthy()
    await sessionSelect.selectOption(matchingSession!)

    // Verify submit button is disabled without a file
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
    const main = page.getByRole('main')
    const emptyMessage = main.getByText('No sessions match the current filters.')
    const reviewLinks = main.getByRole('link', { name: /Review/ })
    const hasItems = await reviewLinks.count()

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
