import { useState, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useReviewQueue } from '../hooks/useReviewQueue'
import { Badge } from '../components/ui/Badge'
import { Button } from '../components/ui/Button'
import { Spinner } from '../components/ui/Spinner'
import type { ReviewQueueItem, ReviewStatus } from '../types'

const statusVariant: Record<ReviewStatus, string> = {
  NotFlagged: 'default',
  Pending: 'pending',
  Approved: 'approved',
  Dismissed: 'dismissed',
}

type SortKey = 'sessionDate' | 'overallConfidence' | 'reviewStatus'
type SortDir = 'asc' | 'desc'

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

export function ReviewQueue() {
  const [statusFilter, setStatusFilter] = useState<string>('Pending')
  const [sortKey, setSortKey] = useState<SortKey>('sessionDate')
  const [sortDir, setSortDir] = useState<SortDir>('desc')

  const { data, isLoading, error } = useReviewQueue(statusFilter || undefined)

  const sorted = useMemo(() => {
    if (!data) return []
    return [...data].sort((a, b) => {
      let cmp = 0
      if (sortKey === 'sessionDate') cmp = a.sessionDate.localeCompare(b.sessionDate)
      else if (sortKey === 'overallConfidence') cmp = a.overallConfidence - b.overallConfidence
      else if (sortKey === 'reviewStatus') cmp = a.reviewStatus.localeCompare(b.reviewStatus)
      return sortDir === 'desc' ? -cmp : cmp
    })
  }, [data, sortKey, sortDir])

  function toggleSort(key: SortKey) {
    if (sortKey === key) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc')
    } else {
      setSortKey(key)
      setSortDir('desc')
    }
  }

  function sortIndicator(key: SortKey) {
    if (sortKey !== key) return ''
    return sortDir === 'asc' ? ' \u2191' : ' \u2193'
  }

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load review queue: {(error as Error).message}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-gray-900">Review Queue</h2>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        >
          <option value="">All</option>
          <option value="Pending">Pending</option>
          <option value="Approved">Approved</option>
          <option value="Dismissed">Dismissed</option>
        </select>
      </div>

      {sorted.length === 0 ? (
        <div className="rounded-md bg-gray-50 p-8 text-center text-sm text-gray-500">
          No sessions match the current filters.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr className="text-left text-xs font-medium uppercase text-gray-500">
                <th className="px-4 py-3">Patient Name</th>
                <th className="cursor-pointer px-4 py-3" onClick={() => toggleSort('sessionDate')}>
                  Session Date{sortIndicator('sessionDate')}
                </th>
                <th className="cursor-pointer px-4 py-3" onClick={() => toggleSort('overallConfidence')}>
                  Confidence{sortIndicator('overallConfidence')}
                </th>
                <th className="px-4 py-3">Review Reasons</th>
                <th className="cursor-pointer px-4 py-3" onClick={() => toggleSort('reviewStatus')}>
                  Status{sortIndicator('reviewStatus')}
                </th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {sorted.map((item: ReviewQueueItem) => (
                <tr key={item.extractionId} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">{item.patientName}</td>
                  <td className="whitespace-nowrap px-4 py-3">{formatDate(item.sessionDate)}</td>
                  <td className="whitespace-nowrap px-4 py-3">{Math.round(item.overallConfidence * 100)}%</td>
                  <td className="max-w-xs truncate px-4 py-3" title={item.reviewReasons.join('; ')}>
                    {item.reviewReasons.length > 0 ? item.reviewReasons.join('; ') : '\u2014'}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <Badge variant={statusVariant[item.reviewStatus]}>{item.reviewStatus}</Badge>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <Link to={`/review/session/${item.sessionId}`}>
                      <Button variant="secondary" className="text-xs">Review &rarr;</Button>
                    </Link>
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
