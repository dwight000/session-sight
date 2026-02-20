import { useMutation, useQueryClient } from '@tanstack/react-query'
import { getSessionSummary } from '../api/sessionSummary'

export function useRegenerateSessionSummary(sessionId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => getSessionSummary(sessionId, true),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reviewDetail', sessionId] })
    },
  })
}
