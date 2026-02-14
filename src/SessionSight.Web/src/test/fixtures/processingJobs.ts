import type { ProcessingJob } from '../../types'

export const mockProcessingJobs: ProcessingJob[] = [
  {
    id: 'job-1',
    jobKey: 'extraction-session-001',
    status: 'Completed',
    createdAt: '2025-01-15T10:00:00Z',
    completedAt: '2025-01-15T10:05:00Z',
  },
  {
    id: 'job-2',
    jobKey: 'extraction-session-002',
    status: 'Processing',
    createdAt: '2025-01-15T11:00:00Z',
    completedAt: null,
  },
  {
    id: 'job-3',
    jobKey: 'extraction-session-003',
    status: 'Failed',
    createdAt: '2025-01-14T09:00:00Z',
    completedAt: '2025-01-14T09:03:00Z',
  },
]
