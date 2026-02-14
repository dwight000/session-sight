import { fetchApi } from './client'
import type { ProcessingJob } from '../types'

export function getProcessingJobs(): Promise<ProcessingJob[]> {
  return fetchApi<ProcessingJob[]>('/api/processing-jobs')
}
