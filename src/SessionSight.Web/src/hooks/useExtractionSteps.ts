import { useQuery } from '@tanstack/react-query'
import { getExtractionSteps } from '../api/extractionSteps'
import type { ExtractionStepsResponse } from '../types/extractionSteps'
import { STEP_ORDER } from '../components/extraction/stepConfig'

const TERMINAL_STATUSES = new Set(['Succeeded', 'Failed', 'Skipped'])

function isPipelineFinished(data: ExtractionStepsResponse | undefined): boolean {
  if (!data || data.steps.length === 0) return false
  // Backend crashed or pipeline failed — document status is the source of truth
  if (data.documentStatus === 'Failed' || data.documentStatus === 'Completed') return true
  if (data.steps.some((s) => s.status === 'Failed')) return true
  // All expected steps must be present AND terminal — don't stop early when
  // only the first few steps have completed but later steps haven't started yet.
  return data.steps.length >= STEP_ORDER.length &&
    data.steps.every((s) => TERMINAL_STATUSES.has(s.status))
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
