import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createSession } from '../api/sessions'
import type { CreateSessionRequest } from '../types'

export function useCreateSession() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CreateSessionRequest) => createSession(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
    },
  })
}
