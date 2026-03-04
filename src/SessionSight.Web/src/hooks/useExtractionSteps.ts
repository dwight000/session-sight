import { useQuery } from '@tanstack/react-query'
import { getExtractionSteps } from '../api/extractionSteps'
import type { ExtractionStepsResponse } from '../types/extractionSteps'

function isPipelineFinished(data: ExtractionStepsResponse | undefined): boolean {
  if (!data || data.steps.length === 0) return false
  // Backend crashed or pipeline finished — document status is the source of truth
  if (data.documentStatus === 'Failed' || data.documentStatus === 'Completed' || data.documentStatus === 'PartiallyCompleted') return true
  if (data.steps.some((s) => s.status === 'Failed')) return true
  // Don't infer finished from step statuses — wait for documentStatus to be
  // terminal. SaveExtractionAsync writes final data BEFORE MarkCompleted sets
  // documentStatus, so stopping early would miss the final data.
  return false
}

export function useExtractionSteps(sessionId: string, isLive: boolean) {
  return useQuery({
    queryKey: ['extractionSteps', sessionId],
    queryFn: () => getExtractionSteps(sessionId),
    enabled: !!sessionId,
    retry: (failureCount, error) => {
      if ((error as Error).message.includes('API 404')) return isLive && failureCount < 3
      return failureCount < 3
    },
    refetchInterval: (query) => {
      if (!isLive) return false
      return isPipelineFinished(query.state.data) ? false : 2000
    },
  })
}
