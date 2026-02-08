import { fetchApi } from './client'
import type { ReviewQueueItem, ReviewDetail, ReviewStats, SubmitReviewRequest } from '../types'

export function getReviewQueue(params?: {
  status?: string
  startDate?: string
  endDate?: string
}): Promise<ReviewQueueItem[]> {
  const qs = new URLSearchParams()
  if (params?.status) qs.set('status', params.status)
  if (params?.startDate) qs.set('startDate', params.startDate)
  if (params?.endDate) qs.set('endDate', params.endDate)
  const query = qs.toString()
  const url = query ? `/api/review/queue?${query}` : '/api/review/queue'
  return fetchApi<ReviewQueueItem[]>(url)
}

export function getReviewDetail(sessionId: string): Promise<ReviewDetail> {
  return fetchApi<ReviewDetail>(`/api/review/session/${sessionId}`)
}

export function submitReview(sessionId: string, body: SubmitReviewRequest): Promise<void> {
  return fetchApi<void>(`/api/review/session/${sessionId}`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function getReviewStats(): Promise<ReviewStats> {
  return fetchApi<ReviewStats>('/api/review/stats')
}
