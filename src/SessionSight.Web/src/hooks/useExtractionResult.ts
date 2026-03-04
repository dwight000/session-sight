import { useQuery } from '@tanstack/react-query'
import { getExtractionResult } from '../api/extractionSteps'

export function useExtractionResult(sessionId: string, enabled: boolean, pipelineFinished = true) {
  return useQuery({
    queryKey: ['extractionResult', sessionId],
    queryFn: () => getExtractionResult(sessionId),
    enabled: !!sessionId && enabled,
    staleTime: pipelineFinished ? 0 : Infinity,
    retry: 1,
  })
}
