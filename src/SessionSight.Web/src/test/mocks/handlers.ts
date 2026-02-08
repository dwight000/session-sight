import { http, HttpResponse } from 'msw'
import { mockReviewQueue, mockReviewDetail, mockReviewStats } from '../fixtures/review'
import { mockPracticeSummary } from '../fixtures/summary'
import { mockPatients } from '../fixtures/patients'
import { mockSessions } from '../fixtures/sessions'

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
      createdAt: '2025-01-03T00:00:00Z',
      updatedAt: '2025-01-03T00:00:00Z'
    })
  }),

  // Upload handlers
  http.post('/api/sessions/:sessionId/document', () => {
    return HttpResponse.json({
      documentId: 'new-doc-id',
      sessionId: 's1',
      fileName: 'test.pdf',
      blobUri: 'blob://test',
      status: 'Pending'
    })
  }),

  http.post('/api/extraction/:sessionId', () => {
    return HttpResponse.json({
      success: true,
      extractionId: 'new-extraction-id'
    })
  }),
]
