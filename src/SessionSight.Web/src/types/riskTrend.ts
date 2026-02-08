export interface RiskTrendPeriod {
  start: string
  end: string
}

export interface PatientRiskTrendPoint {
  sessionId: string
  sessionDate: string
  sessionNumber: number
  riskLevel: string | null
  riskScore: number | null
  moodScore: number | null
  requiresReview: boolean
}

export interface PatientRiskTrend {
  patientId: string
  period: RiskTrendPeriod
  totalSessions: number
  points: PatientRiskTrendPoint[]
  latestRiskLevel: string | null
  hasEscalation: boolean
}
