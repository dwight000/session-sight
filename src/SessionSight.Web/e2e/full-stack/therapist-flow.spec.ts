import { test, expect } from '@playwright/test'

test.describe('Therapist CRUD Flow', () => {
  test('complete therapist CRUD + session creation flow', async ({ page }) => {
    const timestamp = Date.now()
    const therapistName = `E2E Therapist ${timestamp}`

    await test.step('Seed therapist exists', async () => {
      await page.goto('/therapists')
      await expect(page.getByRole('heading', { name: 'Therapists' })).toBeVisible()
      await expect(page.getByText('Default Therapist')).toBeVisible()
    })

    await test.step('Create new therapist', async () => {
      await page.getByRole('button', { name: 'Add Therapist' }).click()
      await page.getByLabel('Name').fill(therapistName)
      await page.getByLabel('License Number').fill('LIC-E2E')
      await page.getByLabel('Credentials').fill('PhD')
      await page.getByRole('button', { name: 'Create Therapist' }).click()

      // Verify form closes and new therapist appears
      await expect(page.getByText(therapistName)).toBeVisible()
    })

    await test.step('Create session with new therapist', async () => {
      // First create a patient
      await page.goto('/patients')
      await expect(page.getByRole('heading', { name: 'Patients' })).toBeVisible()

      const patientFirstName = 'E2E'
      const patientLastName = `TherapistTest${timestamp}`
      const externalId = `E2E-T-${timestamp}`

      await page.getByRole('button', { name: 'Add Patient' }).click()
      await page.getByLabel('First Name').fill(patientFirstName)
      await page.getByLabel('Last Name').fill(patientLastName)
      await page.getByLabel('Date of Birth').fill('1990-01-15')
      await page.getByLabel('External ID').fill(externalId)
      await page.getByRole('button', { name: 'Create Patient' }).click()
      await expect(page.getByText(`${patientFirstName} ${patientLastName}`)).toBeVisible()

      // Now create a session with the new therapist
      await page.goto('/sessions')
      await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible()

      await page.getByRole('button', { name: 'Add Session' }).click()

      // Select patient
      const patientSelect = page.getByLabel('Patient')
      const patientOptions = await patientSelect.locator('option').allTextContents()
      const matchingPatient = patientOptions.find((opt) => opt.includes(patientLastName))
      expect(matchingPatient).toBeTruthy()
      await patientSelect.selectOption(matchingPatient!)

      // Select therapist
      const therapistSelect = page.getByLabel('Therapist')
      const therapistOptions = await therapistSelect.locator('option').allTextContents()
      const matchingTherapist = therapistOptions.find((opt) => opt.includes(therapistName))
      expect(matchingTherapist).toBeTruthy()
      await therapistSelect.selectOption(matchingTherapist!)

      await page.getByLabel('Session Date').fill('2026-01-15')
      await page.getByLabel('Session Type').selectOption('Individual')
      await page.getByLabel('Modality').selectOption('InPerson')
      await page.getByLabel('Session Number').fill('1')

      await page.getByRole('button', { name: 'Create Session' }).click()

      const table = page.locator('table')
      await expect(table.getByText(`${patientFirstName} ${patientLastName}`)).toBeVisible()
    })

    await test.step('Jobs page accessible', async () => {
      await page.goto('/jobs')
      await expect(page.getByRole('heading', { name: 'Processing Jobs' })).toBeVisible()
    })
  })
})
