import { fetchApi } from './client'
import type { QAResponse } from '../types'

export function askQuestion(patientId: string, request: { question: string }): Promise<QAResponse> {
  return fetchApi<QAResponse>(`/api/qa/patient/${encodeURIComponent(patientId)}`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
