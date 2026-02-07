import type { PracticeSummary } from '../../types'

export const mockPracticeSummary: PracticeSummary = {
  period: { start: '2024-12-20', end: '2025-01-19' },
  totalPatients: 24,
  totalSessions: 87,
  sessionsRequiringReview: 3,
  flaggedPatientCount: 2,
  flaggedPatients: [
    {
      patientId: 'pat-001',
      patientIdentifier: 'Alice Johnson',
      highestRiskLevel: 'High',
      flaggedSessionCount: 2,
      lastSessionDate: '2025-01-15',
      flagReason: 'Passive suicidal ideation reported in two consecutive sessions',
    },
    {
      patientId: 'pat-002',
      patientIdentifier: 'David Kim',
      highestRiskLevel: 'Moderate',
      flaggedSessionCount: 1,
      lastSessionDate: '2025-01-12',
      flagReason: 'Significant PHQ-9 score increase',
    },
  ],
  riskDistribution: { low: 60, moderate: 20, high: 5, imminent: 2 },
  averageSessionsPerPatient: 3.6,
  topInterventions: [
    { intervention: 'CBT', count: 45 },
    { intervention: 'Mindfulness', count: 30 },
  ],
  generatedAt: '2025-01-19T12:00:00Z',
}

export const mockEmptyPracticeSummary: PracticeSummary = {
  period: { start: '2024-12-20', end: '2025-01-19' },
  totalPatients: 0,
  totalSessions: 0,
  sessionsRequiringReview: 0,
  flaggedPatientCount: 0,
  flaggedPatients: [],
  riskDistribution: { low: 0, moderate: 0, high: 0, imminent: 0 },
  averageSessionsPerPatient: 0,
  topInterventions: [],
  generatedAt: '2025-01-19T12:00:00Z',
}
