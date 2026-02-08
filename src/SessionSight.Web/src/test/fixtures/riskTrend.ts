import type { PatientRiskTrend } from '../../types/riskTrend'

export const mockPatientRiskTrend: PatientRiskTrend = {
  patientId: 'pat-001',
  period: { start: '2024-12-20', end: '2025-01-19' },
  totalSessions: 4,
  points: [
    {
      sessionId: 'sess-001',
      sessionDate: '2024-12-23',
      sessionNumber: 1,
      riskLevel: 'Low',
      riskScore: 0,
      moodScore: 5,
      requiresReview: false,
    },
    {
      sessionId: 'sess-002',
      sessionDate: '2024-12-30',
      sessionNumber: 2,
      riskLevel: 'Moderate',
      riskScore: 1,
      moodScore: 4,
      requiresReview: true,
    },
    {
      sessionId: 'sess-003',
      sessionDate: '2025-01-07',
      sessionNumber: 3,
      riskLevel: 'High',
      riskScore: 2,
      moodScore: 3,
      requiresReview: true,
    },
    {
      sessionId: 'sess-004',
      sessionDate: '2025-01-15',
      sessionNumber: 4,
      riskLevel: 'Moderate',
      riskScore: 1,
      moodScore: 4,
      requiresReview: true,
    },
  ],
  latestRiskLevel: 'Moderate',
  hasEscalation: true,
}
