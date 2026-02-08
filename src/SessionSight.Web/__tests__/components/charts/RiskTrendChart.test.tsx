import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { RiskTrendChart } from '../../../src/components/charts/RiskTrendChart'
import type { PatientRiskTrendPoint } from '../../../src/types/riskTrend'

describe('RiskTrendChart', () => {
  it('shows empty message when no points provided', () => {
    render(<RiskTrendChart points={[]} />)
    expect(screen.getByText(/no sessions in this date range/i)).toBeInTheDocument()
  })

  it('shows no risk data message when all points have null riskScore', () => {
    const points: PatientRiskTrendPoint[] = [
      { sessionId: 's1', sessionNumber: 1, sessionDate: '2025-01-01', riskScore: null, riskLevel: null, moodScore: null, requiresReview: false },
      { sessionId: 's2', sessionNumber: 2, sessionDate: '2025-01-08', riskScore: null, riskLevel: null, moodScore: null, requiresReview: false },
    ]
    render(<RiskTrendChart points={points} />)
    expect(screen.getByText(/no extracted risk levels available/i)).toBeInTheDocument()
  })

  it('renders chart with valid points', () => {
    const points: PatientRiskTrendPoint[] = [
      { sessionId: 's1', sessionNumber: 1, sessionDate: '2025-01-01', riskScore: 1, riskLevel: 'Moderate', moodScore: 5, requiresReview: false },
      { sessionId: 's2', sessionNumber: 2, sessionDate: '2025-01-08', riskScore: 2, riskLevel: 'High', moodScore: 4, requiresReview: true },
    ]
    render(<RiskTrendChart points={points} />)
    expect(screen.getByRole('img', { name: /risk trend chart/i })).toBeInTheDocument()
  })

  it('skips null points in the line but still shows dates', () => {
    const points: PatientRiskTrendPoint[] = [
      { sessionId: 's1', sessionNumber: 1, sessionDate: '2025-01-01', riskScore: 1, riskLevel: 'Moderate', moodScore: 5, requiresReview: false },
      { sessionId: 's2', sessionNumber: 2, sessionDate: '2025-01-08', riskScore: null, riskLevel: null, moodScore: null, requiresReview: false },
      { sessionId: 's3', sessionNumber: 3, sessionDate: '2025-01-15', riskScore: 2, riskLevel: 'High', moodScore: 4, requiresReview: true },
    ]
    render(<RiskTrendChart points={points} />)
    // Chart should render (has 2 valid points)
    expect(screen.getByRole('img', { name: /risk trend chart/i })).toBeInTheDocument()
    // Start and end dates should be shown
    expect(screen.getByText(/start:/i)).toBeInTheDocument()
    expect(screen.getByText(/end:/i)).toBeInTheDocument()
  })
})
