import { useMutation, useQueryClient } from '@tanstack/react-query'
import { triggerExtraction } from '../api/upload'

export function useRetryExtraction() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (sessionId: string) => triggerExtraction(sessionId),
    onSuccess: (_data, sessionId) => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
      queryClient.invalidateQueries({ queryKey: ['extractionSteps', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['extractionResult', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['reviewDetail', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['reviewQueue'] })
    },
  })
}
