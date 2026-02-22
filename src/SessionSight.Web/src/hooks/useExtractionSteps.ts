import { useQuery } from '@tanstack/react-query'
import { getExtractionSteps } from '../api/extractionSteps'
import type { ExtractionStepsResponse } from '../types/extractionSteps'

const TERMINAL_STATUSES = new Set(['Succeeded', 'Failed', 'Skipped'])

function isPipelineFinished(data: ExtractionStepsResponse | undefined): boolean {
  if (!data || data.steps.length === 0) return false
  return data.steps.every((s) => TERMINAL_STATUSES.has(s.status)) || data.steps.some((s) => s.status === 'Failed')
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
