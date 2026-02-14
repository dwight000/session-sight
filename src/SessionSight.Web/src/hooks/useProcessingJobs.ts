import { useQuery } from '@tanstack/react-query'
import { getProcessingJobs } from '../api/processingJobs'
import type { ProcessingJob } from '../types'

export function useProcessingJobs() {
  return useQuery({
    queryKey: ['processing-jobs'],
    queryFn: getProcessingJobs,
    select: (data) => data,
    refetchInterval: (query) => {
      const jobs = query.state.data as ProcessingJob[] | undefined
      if (!jobs) return false
      const hasActiveJobs = jobs.some(j => j.status === 'Pending' || j.status === 'Processing')
      return hasActiveJobs ? 5000 : false
    },
  })
}
