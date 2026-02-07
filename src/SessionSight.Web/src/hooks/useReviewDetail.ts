import { useQuery } from '@tanstack/react-query'
import { getReviewDetail } from '../api/review'

export function useReviewDetail(sessionId: string) {
  return useQuery({
    queryKey: ['reviewDetail', sessionId],
    queryFn: () => getReviewDetail(sessionId),
    enabled: !!sessionId,
  })
}
