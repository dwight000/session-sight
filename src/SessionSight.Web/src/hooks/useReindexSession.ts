import { useMutation, useQueryClient } from '@tanstack/react-query'
import { reindexSession } from '../api/admin'

export function useReindexSession() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (sessionId: string) => reindexSession(sessionId),
    onSuccess: (_data, sessionId) => {
      queryClient.invalidateQueries({ queryKey: ['reviewDetail', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
    },
  })
}
