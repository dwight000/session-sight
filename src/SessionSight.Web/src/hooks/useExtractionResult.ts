import { useQuery } from '@tanstack/react-query'
import { getExtractionResult } from '../api/extractionSteps'

export function useExtractionResult(sessionId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['extractionResult', sessionId],
    queryFn: () => getExtractionResult(sessionId),
    enabled: !!sessionId && enabled,
    staleTime: 0,
    retry: 1,
  })
}
