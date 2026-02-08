import { describe, it, expect } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { Route, Routes } from 'react-router-dom'
import { server } from '../../src/test/mocks/server'
import { renderWithProviders } from '../../src/test/render'
import { PatientTimeline } from '../../src/pages/PatientTimeline'
import { mockPatientTimeline } from '../../src/test/fixtures/timeline'

function renderPatientTimeline(patientId = 'p1') {
  return renderWithProviders(
    <Routes>
      <Route path="/patients/:patientId/timeline" element={<PatientTimeline />} />
    </Routes>,
    { route: `/patients/${patientId}/timeline` },
  )
}

describe('PatientTimeline', () => {
  it('renders timeline metadata and entries', async () => {
    renderPatientTimeline()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Patient Timeline' })).toBeInTheDocument()
    })

    expect(screen.getByText('John Doe (EXT001)')).toBeInTheDocument()
    expect(screen.getByText('Session Timeline')).toBeInTheDocument()
    expect(screen.getByText(`Sessions: ${mockPatientTimeline.totalSessions}`)).toBeInTheDocument()
    expect(screen.getByText('Escalation detected')).toBeInTheDocument()
    expect(screen.getByText('Session 1 · Individual')).toBeInTheDocument()
    expect(screen.getByText('Session 3 · Crisis')).toBeInTheDocument()
    expect(screen.getAllByText('Review →').length).toBeGreaterThan(0)
  })

  it('renders empty state when no sessions are returned', async () => {
    server.use(
      http.get('/api/summary/patient/:patientId/timeline', ({ params }) => {
        return HttpResponse.json({
          ...mockPatientTimeline,
          patientId: params.patientId,
          totalSessions: 0,
          entries: [],
          latestRiskLevel: null,
          hasEscalation: false,
        })
      }),
    )

    renderPatientTimeline()

    await waitFor(() => {
      expect(screen.getByText('No sessions found in this date range.')).toBeInTheDocument()
    })
  })
})
