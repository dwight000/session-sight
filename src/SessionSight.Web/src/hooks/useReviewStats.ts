import { useQuery } from '@tanstack/react-query'
import { getReviewStats } from '../api/review'

export function useReviewStats() {
  return useQuery({
    queryKey: ['reviewStats'],
    queryFn: getReviewStats,
  })
}
