export interface PatientTimelineEntry {
  sessionId: string
  sessionDate: string
  sessionNumber: number
  sessionType: string
  modality: string
  hasDocument: boolean
  documentStatus: string | null
  documentFileName: string | null
  documentBlobUri: string | null
  riskLevel: string | null
  riskScore: number | null
  moodScore: number | null
  requiresReview: boolean
  reviewStatus: string
  daysSincePreviousSession: number | null
  riskChange: string | null
  moodDelta: number | null
  moodChange: string | null
}

export interface PatientTimeline {
  patientId: string
  startDate: string | null
  endDate: string | null
  totalSessions: number
  entries: PatientTimelineEntry[]
  latestRiskLevel: string | null
  hasEscalation: boolean
}
