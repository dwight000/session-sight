import { fetchApi } from './client'
import type { ExtractionStepsResponse } from '../types/extractionSteps'
import type { ExtractionResultResponse } from '../types'

export function getExtractionSteps(sessionId: string): Promise<ExtractionStepsResponse> {
  return fetchApi<ExtractionStepsResponse>(`/api/sessions/${encodeURIComponent(sessionId)}/extraction/steps`)
}

export function getExtractionResult(sessionId: string): Promise<ExtractionResultResponse> {
  return fetchApi<ExtractionResultResponse>(`/api/sessions/${encodeURIComponent(sessionId)}/extraction`)
}
