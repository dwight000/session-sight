import { useProcessingJobs } from '../hooks/useProcessingJobs'
import { Badge } from '../components/ui/Badge'
import { Spinner } from '../components/ui/Spinner'
import type { ProcessingJob, JobStatus } from '../types'

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString()
}

function statusVariant(status: JobStatus): string {
  switch (status) {
    case 'Pending': return 'pending'
    case 'Processing': return 'pending'
    case 'Completed': return 'approved'
    case 'Failed': return 'danger'
    default: return 'default'
  }
}

export function ProcessingJobs() {
  const { data: jobs, isLoading, error } = useProcessingJobs()

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load processing jobs: {(error as Error).message}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-900">Processing Jobs</h2>
      </div>

      {jobs?.length === 0 ? (
        <div className="rounded-md bg-gray-50 p-8 text-center text-sm text-gray-500">
          No processing jobs found.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr className="text-left text-xs font-medium uppercase text-gray-500">
                <th className="px-4 py-3">Job Key</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3">Completed</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {jobs?.map((job: ProcessingJob) => (
                <tr key={job.id} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">
                    {job.jobKey}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <Badge variant={statusVariant(job.status)}>
                      {job.status}
                    </Badge>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {formatDateTime(job.createdAt)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {job.completedAt ? formatDateTime(job.completedAt) : '\u2014'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
