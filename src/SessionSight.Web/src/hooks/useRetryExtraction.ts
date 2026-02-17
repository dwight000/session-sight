import { useMutation, useQueryClient } from '@tanstack/react-query'
import { triggerExtraction } from '../api/upload'

export function useRetryExtraction() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (sessionId: string) => triggerExtraction(sessionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
    },
  })
}
