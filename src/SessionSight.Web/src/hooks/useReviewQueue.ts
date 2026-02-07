import { useQuery } from '@tanstack/react-query'
import { getReviewQueue } from '../api/review'

export function useReviewQueue(status?: string, startDate?: string, endDate?: string) {
  return useQuery({
    queryKey: ['reviewQueue', status, startDate, endDate],
    queryFn: () => getReviewQueue({ status, startDate, endDate }),
  })
}
