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
      sessionDate: { value: '2025-01-15', confidence: 0.98, source: { text: 'Header of note', startChar: 0, endChar: 50, section: 'header' } },
      sessionType: { value: 'Individual therapy', confidence: 0.95, source: { text: 'Note body', startChar: 100, endChar: 150, section: 'body' } },
      duration: { value: '50 minutes', confidence: 0.9, source: { text: 'Note body', startChar: 200, endChar: 250, section: 'body' } },
    },
    presentingConcerns: {
      primaryConcern: { value: 'Anxiety with panic attacks', confidence: 0.92, source: { text: 'Presenting problem section', startChar: 300, endChar: 400, section: 'presenting' } },
      symptoms: { value: ['Panic attacks', 'Insomnia', 'Racing thoughts'], confidence: 0.88, source: { text: 'Symptom checklist', startChar: 400, endChar: 500, section: 'symptoms' } },
    },
    moodAssessment: {
      currentMood: { value: 'Anxious', confidence: 0.95, source: { text: 'Mood assessment', startChar: 500, endChar: 600, section: 'mood' } },
      phq9Score: { value: 12, confidence: 0.85, source: { text: 'PHQ-9 section', startChar: 600, endChar: 700, section: 'assessment' } },
    },
    riskAssessment: {
      suicidalIdeation: { value: 'Passive', confidence: 0.65, source: { text: 'Risk section', startChar: 700, endChar: 800, section: 'risk' } },
      overallRiskLevel: { value: 'Moderate', confidence: 0.7, source: { text: 'Risk section', startChar: 800, endChar: 900, section: 'risk' } },
    },
    mentalStatusExam: {
      appearance: { value: 'Well-groomed, appropriate dress', confidence: 0.9, source: { text: 'MSE section', startChar: 900, endChar: 1000, section: 'mse' } },
      affect: { value: 'Anxious, congruent with mood', confidence: 0.88, source: { text: 'MSE section', startChar: 1000, endChar: 1100, section: 'mse' } },
    },
    interventions: {
      techniquesUsed: { value: ['CBT', 'Breathing exercises'], confidence: 0.92, source: { text: 'Interventions section', startChar: 1100, endChar: 1200, section: 'interventions' } },
    },
    diagnoses: {
      primaryDiagnosis: { value: 'Generalized Anxiety Disorder', confidence: 0.9, source: { text: 'Diagnosis section', startChar: 1200, endChar: 1300, section: 'diagnosis' } },
    },
    treatmentProgress: {
      progressRating: { value: 'Moderate improvement', confidence: 0.8, source: { text: 'Progress section', startChar: 1300, endChar: 1400, section: 'progress' } },
    },
    nextSteps: {
      followUpPlan: { value: 'Weekly sessions, expand coping skills', confidence: 0.85, source: { text: 'Plan section', startChar: 1400, endChar: 1500, section: 'plan' } },
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
