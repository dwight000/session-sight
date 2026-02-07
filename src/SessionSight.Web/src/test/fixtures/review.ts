import type { ReviewQueueItem, ReviewDetail, ReviewStats } from '../../types'

export const mockReviewStats: ReviewStats = {
  pendingCount: 3,
  approvedToday: 5,
  dismissedToday: 1,
}

export const mockReviewQueue: ReviewQueueItem[] = [
  {
    extractionId: 'ext-001',
    sessionId: 'sess-001',
    patientName: 'Alice Johnson',
    sessionDate: '2025-01-15',
    reviewStatus: 'Pending',
    overallConfidence: 0.72,
    reviewReasons: ['Low confidence on risk assessment', 'Suicide ideation mentioned'],
    extractedAt: '2025-01-15T14:30:00Z',
  },
  {
    extractionId: 'ext-002',
    sessionId: 'sess-002',
    patientName: 'Bob Smith',
    sessionDate: '2025-01-14',
    reviewStatus: 'Pending',
    overallConfidence: 0.85,
    reviewReasons: ['New patient intake'],
    extractedAt: '2025-01-14T10:00:00Z',
  },
  {
    extractionId: 'ext-003',
    sessionId: 'sess-003',
    patientName: 'Carol Davis',
    sessionDate: '2025-01-16',
    reviewStatus: 'Approved',
    overallConfidence: 0.95,
    reviewReasons: [],
    extractedAt: '2025-01-16T09:15:00Z',
  },
]

export const mockReviewDetail: ReviewDetail = {
  extractionId: 'ext-001',
  sessionId: 'sess-001',
  patientName: 'Alice Johnson',
  sessionDate: '2025-01-15',
  reviewStatus: 'Pending',
  overallConfidence: 0.72,
  requiresReview: true,
  reviewReasons: ['Low confidence on risk assessment', 'Suicide ideation mentioned'],
  summaryJson: JSON.stringify({
    sessionId: 'sess-001',
    oneLiner: 'Patient discussed ongoing anxiety and recent panic attacks.',
    keyPoints: 'Reported 3 panic attacks this week. Sleep disrupted. Using breathing exercises with partial success.',
    interventionsUsed: ['CBT', 'Breathing exercises', 'Psychoeducation'],
    nextSessionFocus: 'Develop expanded coping toolkit and review sleep hygiene.',
    riskFlags: {
      riskLevel: 'Moderate',
      flags: ['Passive suicidal ideation reported'],
      requiresReview: true,
    },
    modelUsed: 'gpt-4o',
    generatedAt: '2025-01-15T15:00:00Z',
  }),
  data: {
    sessionInfo: {
      sessionDate: { value: '2025-01-15', confidence: 0.98, source: 'Header of note' },
      sessionType: { value: 'Individual therapy', confidence: 0.95, source: 'Note body' },
      duration: { value: '50 minutes', confidence: 0.9, source: 'Note body' },
    },
    presentingConcerns: {
      primaryConcern: { value: 'Anxiety with panic attacks', confidence: 0.92, source: 'Presenting problem section' },
      symptoms: { value: ['Panic attacks', 'Insomnia', 'Racing thoughts'], confidence: 0.88, source: 'Symptom checklist' },
    },
    moodAssessment: {
      currentMood: { value: 'Anxious', confidence: 0.95, source: 'Mood assessment' },
      phq9Score: { value: 12, confidence: 0.85, source: 'PHQ-9 section' },
    },
    riskAssessment: {
      suicidalIdeation: { value: 'Passive', confidence: 0.65, source: 'Risk section' },
      overallRiskLevel: { value: 'Moderate', confidence: 0.7, source: 'Risk section' },
    },
    mentalStatusExam: {
      appearance: { value: 'Well-groomed, appropriate dress', confidence: 0.9, source: 'MSE section' },
      affect: { value: 'Anxious, congruent with mood', confidence: 0.88, source: 'MSE section' },
    },
    interventions: {
      techniquesUsed: { value: ['CBT', 'Breathing exercises'], confidence: 0.92, source: 'Interventions section' },
    },
    diagnoses: {
      primaryDiagnosis: { value: 'Generalized Anxiety Disorder', confidence: 0.9, source: 'Diagnosis section' },
    },
    treatmentProgress: {
      progressRating: { value: 'Moderate improvement', confidence: 0.8, source: 'Progress section' },
    },
    nextSteps: {
      followUpPlan: { value: 'Weekly sessions, expand coping skills', confidence: 0.85, source: 'Plan section' },
    },
  },
  reviews: [
    {
      id: 'rev-001',
      action: 'Pending',
      reviewerName: 'Dr. Martinez',
      notes: 'Flagged for risk assessment review.',
      reviewedAt: '2025-01-15T16:00:00Z',
    },
  ],
}

export const mockApprovedDetail: ReviewDetail = {
  ...mockReviewDetail,
  reviewStatus: 'Approved',
  reviews: [
    ...mockReviewDetail.reviews,
    {
      id: 'rev-002',
      action: 'Approved',
      reviewerName: 'Dr. Chen',
      notes: 'Risk assessment confirmed. Safety plan in place.',
      reviewedAt: '2025-01-16T09:00:00Z',
    },
  ],
}
