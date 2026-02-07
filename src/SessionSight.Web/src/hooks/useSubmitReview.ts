import { useMutation, useQueryClient } from '@tanstack/react-query'
import { submitReview } from '../api/review'
import type { SubmitReviewRequest } from '../types'

export function useSubmitReview(sessionId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: SubmitReviewRequest) => submitReview(sessionId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reviewDetail', sessionId] })
      queryClient.invalidateQueries({ queryKey: ['reviewQueue'] })
      queryClient.invalidateQueries({ queryKey: ['reviewStats'] })
    },
  })
}
