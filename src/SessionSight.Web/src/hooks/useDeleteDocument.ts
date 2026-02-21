import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteDocument } from '../api/upload'

export function useDeleteDocument(sessionId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => deleteDocument(sessionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
      queryClient.invalidateQueries({ queryKey: ['reviewDetail', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['reviewQueue'] })
    },
  })
}
