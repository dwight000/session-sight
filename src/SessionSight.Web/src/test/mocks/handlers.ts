import { http, HttpResponse } from 'msw'
import { mockReviewQueue, mockReviewDetail, mockReviewStats } from '../fixtures/review'
import { mockPatientSummary, mockPracticeSummary, mockSessionSummary } from '../fixtures/summary'
import { mockPatients } from '../fixtures/patients'
import { mockSessions } from '../fixtures/sessions'
import { mockPatientRiskTrend } from '../fixtures/riskTrend'
import { mockPatientTimeline } from '../fixtures/timeline'
import { mockTherapists } from '../fixtures/therapists'
import { mockProcessingJobs } from '../fixtures/processingJobs'
import { mockQAResponse } from '../fixtures/qa'
import { mockExtractionStepsComplete, mockExtractionResult } from '../fixtures/extractionSteps'

export const handlers = [
  http.get('/api/review/queue', () => {
    return HttpResponse.json(mockReviewQueue)
  }),

  http.get('/api/review/session/:sessionId', () => {
    return HttpResponse.json(mockReviewDetail)
  }),

  http.post('/api/review/session/:sessionId', () => {
    return new HttpResponse(null, { status: 200 })
  }),

  http.get('/api/review/stats', () => {
    return HttpResponse.json(mockReviewStats)
  }),

  http.get('/api/summary/practice', () => {
    return HttpResponse.json(mockPracticeSummary)
  }),

  http.get('/api/summary/session/:sessionId', () => {
    return HttpResponse.json(mockSessionSummary)
  }),

  http.get('/api/summary/patient/:patientId/risk-trend', ({ params }) => {
    return HttpResponse.json({
      ...mockPatientRiskTrend,
      patientId: params.patientId,
    })
  }),

  http.get('/api/summary/patient/:patientId/timeline', ({ params }) => {
    return HttpResponse.json({
      ...mockPatientTimeline,
      patientId: params.patientId,
    })
  }),

  http.get('/api/summary/patient/:patientId', ({ params }) => {
    return HttpResponse.json({
      ...mockPatientSummary,
      patientId: params.patientId,
    })
  }),

  // Patient handlers
  http.get('/api/patients', () => {
    return HttpResponse.json(mockPatients)
  }),

  http.get('/api/patients/:id', ({ params }) => {
    const patient = mockPatients.find(p => p.id === params.id)
    if (!patient) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json(patient)
  }),

  http.post('/api/patients', async ({ request }) => {
    const body = await request.json() as Record<string, unknown>
    return HttpResponse.json({
      id: 'new-patient-id',
      ...body,
      createdAt: '2025-01-03T00:00:00Z',
      updatedAt: '2025-01-03T00:00:00Z'
    })
  }),

  // Session handlers
  http.get('/api/sessions', ({ request }) => {
    const url = new URL(request.url)
    const hasDocument = url.searchParams.get('hasDocument')
    const patientId = url.searchParams.get('patientId')

    let filtered = [...mockSessions]
    if (hasDocument === 'false') {
      filtered = filtered.filter(s => !s.hasDocument)
    } else if (hasDocument === 'true') {
      filtered = filtered.filter(s => s.hasDocument)
    }
    if (patientId) {
      filtered = filtered.filter(s => s.patientId === patientId)
    }
    return HttpResponse.json(filtered)
  }),

  http.get('/api/sessions/:id', ({ params }) => {
    const session = mockSessions.find(s => s.id === params.id)
    if (!session) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json(session)
  }),

  http.post('/api/sessions', async ({ request }) => {
    const body = await request.json() as Record<string, unknown>
    return HttpResponse.json({
      id: 'new-session-id',
      ...body,
      hasDocument: false,
      documentStatus: null,
      createdAt: '2025-01-03T00:00:00Z',
      updatedAt: '2025-01-03T00:00:00Z'
    })
  }),

  // Therapist handlers
  http.get('/api/therapists', () => {
    return HttpResponse.json(mockTherapists)
  }),

  http.post('/api/therapists', async ({ request }) => {
    const body = await request.json() as Record<string, unknown>
    return HttpResponse.json({
      id: 'new-therapist-id',
      ...body,
      createdAt: '2025-01-03T00:00:00Z',
      updatedAt: null,
    })
  }),

  // Processing job handlers
  http.get('/api/processing-jobs', () => {
    return HttpResponse.json(mockProcessingJobs)
  }),

  // Upload handlers
  http.delete('/api/sessions/:sessionId/document', () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.post('/api/sessions/:sessionId/document', () => {
    return HttpResponse.json({
      documentId: 'new-doc-id',
      sessionId: 's1',
      originalFileName: 'test.pdf',
      blobUri: 'blob://test',
      status: 'Pending'
    })
  }),

  http.post('/api/extraction/:sessionId', ({ params }) => {
    return HttpResponse.json(
      { sessionId: params.sessionId },
      { status: 202 }
    )
  }),

  // Extraction steps handler
  http.get('/api/sessions/:sessionId/extraction/steps', () => {
    return HttpResponse.json(mockExtractionStepsComplete)
  }),

  // Extraction result handler
  http.get('/api/sessions/:sessionId/extraction', () => {
    return HttpResponse.json(mockExtractionResult)
  }),

  // Q&A handler
  http.post('/api/qa/patient/:patientId', () => {
    return HttpResponse.json(mockQAResponse)
  }),
]
