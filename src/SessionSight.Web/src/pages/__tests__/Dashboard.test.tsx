import { describe, it, expect } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { server } from '../../test/mocks/server'
import { renderWithProviders } from '../../test/render'
import { Dashboard } from '../Dashboard'
import { mockPracticeSummary, mockEmptyPracticeSummary } from '../../test/fixtures/summary'
import { mockReviewStats } from '../../test/fixtures/review'

describe('Dashboard', () => {
  it('renders stats cards with correct values', async () => {
    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText('Dashboard')).toBeInTheDocument()
    })

    expect(screen.getByText(String(mockPracticeSummary.totalSessions))).toBeInTheDocument()
    // pendingCount appears in both the badge and the card value
    expect(screen.getAllByText(String(mockReviewStats.pendingCount)).length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(String(mockPracticeSummary.totalPatients))).toBeInTheDocument()
    expect(screen.getByText(mockPracticeSummary.averageSessionsPerPatient.toFixed(1))).toBeInTheDocument()
  })

  it('shows pending review count badge when > 0', async () => {
    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText('Dashboard')).toBeInTheDocument()
    })

    // The badge shows the pending count as well
    const badges = screen.getAllByText(String(mockReviewStats.pendingCount))
    expect(badges.length).toBeGreaterThanOrEqual(2) // one in card value, one in badge
  })

  it('renders risk distribution bar', async () => {
    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText('Risk Distribution')).toBeInTheDocument()
    })

    expect(screen.getByText(`Low: ${mockPracticeSummary.riskDistribution.low}`)).toBeInTheDocument()
    expect(screen.getByText(`Moderate: ${mockPracticeSummary.riskDistribution.moderate}`)).toBeInTheDocument()
    expect(screen.getByText(`High: ${mockPracticeSummary.riskDistribution.high}`)).toBeInTheDocument()
    expect(screen.getByText(`Imminent: ${mockPracticeSummary.riskDistribution.imminent}`)).toBeInTheDocument()
  })

  it('renders flagged patients table with correct rows', async () => {
    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText('Flagged Patients')).toBeInTheDocument()
    })

    for (const fp of mockPracticeSummary.flaggedPatients) {
      expect(screen.getByText(fp.patientIdentifier)).toBeInTheDocument()
      expect(screen.getByText(fp.flagReason)).toBeInTheDocument()
    }
  })

  it('shows empty state when no flagged patients', async () => {
    server.use(
      http.get('/api/summary/practice', () => {
        return HttpResponse.json(mockEmptyPracticeSummary)
      }),
    )

    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText('No flagged patients in this period.')).toBeInTheDocument()
    })
  })

  it('shows spinner during loading', () => {
    // MSW will delay response, so spinner appears immediately
    server.use(
      http.get('/api/summary/practice', () => {
        return new Promise(() => {}) // never resolves
      }),
    )

    renderWithProviders(<Dashboard />)

    // Spinner is a div with animate-spin class
    const spinner = document.querySelector('.animate-spin')
    expect(spinner).toBeInTheDocument()
  })

  it('shows error message on API failure', async () => {
    server.use(
      http.get('/api/summary/practice', () => {
        return new HttpResponse('Internal Server Error', { status: 500 })
      }),
    )

    renderWithProviders(<Dashboard />)

    await waitFor(() => {
      expect(screen.getByText(/Failed to load dashboard data/)).toBeInTheDocument()
    })
  })
})
