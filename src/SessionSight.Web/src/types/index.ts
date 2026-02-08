// Patient types
export interface Patient {
  id: string
  externalId: string
  firstName: string
  lastName: string
  dateOfBirth: string
  createdAt: string
  updatedAt: string
}

export interface CreatePatientRequest {
  externalId: string
  firstName: string
  lastName: string
  dateOfBirth: string
}

// Session types
export type SessionType = 'Intake' | 'Individual' | 'Group' | 'Family' | 'Couples' | 'Crisis' | 'Assessment' | 'Termination'
export type SessionModality = 'InPerson' | 'TelehealthVideo' | 'TelehealthPhone' | 'Hybrid'

export interface Session {
  id: string
  patientId: string
  therapistId: string
  sessionDate: string
  sessionType: SessionType
  modality: SessionModality
  durationMinutes: number | null
  sessionNumber: number
  hasDocument: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateSessionRequest {
  patientId: string
  therapistId: string
  sessionDate: string
  sessionType: SessionType
  modality: SessionModality
  durationMinutes?: number | null
  sessionNumber: number
}

// Upload types
export interface UploadDocumentResponse {
  documentId: string
  sessionId: string
  fileName: string
  blobUri: string
  status: string
}

// Review types
export type ReviewStatus = 'NotFlagged' | 'Pending' | 'Approved' | 'Dismissed'

export interface ReviewQueueItem {
  extractionId: string
  sessionId: string
  patientName: string
  sessionDate: string
  reviewStatus: ReviewStatus
  overallConfidence: number
  reviewReasons: string[]
  extractedAt: string
}

export interface ReviewDetail {
  extractionId: string
  sessionId: string
  patientName: string
  sessionDate: string
  reviewStatus: ReviewStatus
  overallConfidence: number
  requiresReview: boolean
  reviewReasons: string[]
  summaryJson: string | null
  data: ClinicalExtraction
  reviews: SupervisorReview[]
}

export interface SubmitReviewRequest {
  action: 'Approved' | 'Dismissed'
  reviewerName: string
  notes?: string
}

export interface SupervisorReview {
  id: string
  action: ReviewStatus
  reviewerName: string
  notes: string | null
  reviewedAt: string
}

export interface ReviewStats {
  pendingCount: number
  approvedToday: number
  dismissedToday: number
}

export interface PracticeSummary {
  period: { start: string; end: string }
  totalPatients: number
  totalSessions: number
  sessionsRequiringReview: number
  flaggedPatientCount: number
  flaggedPatients: FlaggedPatientSummary[]
  riskDistribution: { low: number; moderate: number; high: number; imminent: number }
  averageSessionsPerPatient: number
  topInterventions: { intervention: string; count: number }[]
  generatedAt: string
}

export interface FlaggedPatientSummary {
  patientId: string
  patientIdentifier: string
  highestRiskLevel: string
  flaggedSessionCount: number
  lastSessionDate: string
  flagReason: string
}

export interface SessionSummary {
  sessionId: string
  oneLiner: string
  keyPoints: string
  interventionsUsed: string[]
  nextSessionFocus: string
  riskFlags: { riskLevel: string; flags: string[]; requiresReview: boolean } | null
  modelUsed: string
  generatedAt: string
}

export interface ExtractedField<T = unknown> {
  value: T
  confidence: number
  source: string
}

export interface ClinicalExtraction {
  sessionInfo: Record<string, ExtractedField>
  presentingConcerns: Record<string, ExtractedField>
  moodAssessment: Record<string, ExtractedField>
  riskAssessment: Record<string, ExtractedField>
  mentalStatusExam: Record<string, ExtractedField>
  interventions: Record<string, ExtractedField>
  diagnoses: Record<string, ExtractedField>
  treatmentProgress: Record<string, ExtractedField>
  nextSteps: Record<string, ExtractedField>
  metadata?: Record<string, ExtractedField>
}
