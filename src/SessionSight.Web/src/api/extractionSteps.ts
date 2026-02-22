import { fetchApi } from './client'
import type { ExtractionStepsResponse } from '../types/extractionSteps'

export function getExtractionSteps(sessionId: string): Promise<ExtractionStepsResponse> {
  return fetchApi<ExtractionStepsResponse>(`/api/sessions/${encodeURIComponent(sessionId)}/extraction/steps`)
}
