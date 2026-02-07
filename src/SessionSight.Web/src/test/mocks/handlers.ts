import { http, HttpResponse } from 'msw'
import { mockReviewQueue, mockReviewDetail, mockReviewStats } from '../fixtures/review'
import { mockPracticeSummary } from '../fixtures/summary'

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
]
