import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { usePracticeSummary } from '../hooks/usePracticeSummary'
import { useReviewStats } from '../hooks/useReviewStats'
import { usePatientRiskTrend } from '../hooks/usePatientRiskTrend'
import { Card } from '../components/ui/Card'
import { Badge } from '../components/ui/Badge'
import { RiskBadge } from '../components/ui/RiskBadge'
import { Spinner } from '../components/ui/Spinner'
import { RiskTrendChart } from '../components/charts/RiskTrendChart'

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

export function Dashboard() {
  const [selectedPatientId, setSelectedPatientId] = useState('')

  const dateRange = useMemo(() => {
    const end = new Date()
    const start = new Date()
    start.setDate(start.getDate() - 30)
    return {
      start: start.toISOString().slice(0, 10),
      end: end.toISOString().slice(0, 10),
    }
  }, [])

  const { data: summary, isLoading: summaryLoading, error: summaryError } = usePracticeSummary(dateRange.start, dateRange.end)
  const { data: stats, isLoading: statsLoading, error: statsError } = useReviewStats()
  const flaggedPatients = summary?.flaggedPatients ?? []

  useEffect(() => {
    if (flaggedPatients.length === 0) {
      setSelectedPatientId('')
      return
    }

    setSelectedPatientId((current) => {
      if (current && flaggedPatients.some((patient) => patient.patientId === current)) {
        return current
      }
      return flaggedPatients[0].patientId
    })
  }, [flaggedPatients])

  const { data: riskTrend, isLoading: riskTrendLoading, error: riskTrendError } = usePatientRiskTrend(
    selectedPatientId,
    dateRange.start,
    dateRange.end,
  )

  if (summaryLoading || statsLoading) return <Spinner />

  const error = summaryError || statsError
  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load dashboard data: {(error as Error).message}
      </div>
    )
  }

  const risk = summary?.riskDistribution
  const riskTotal = risk ? risk.low + risk.moderate + risk.high + risk.imminent : 0

  return (
    <div className="space-y-6">
      <h2 className="text-2xl font-bold text-gray-900">Dashboard</h2>

      {/* Stats row */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card>
          <p className="text-sm text-gray-500">Total Sessions</p>
          <p className="mt-1 text-3xl font-semibold text-gray-900">{summary?.totalSessions ?? 0}</p>
        </Card>
        <Card>
          <div className="flex items-center gap-2">
            <p className="text-sm text-gray-500">Pending Review</p>
            {(stats?.pendingCount ?? 0) > 0 && <Badge variant="danger">{stats!.pendingCount}</Badge>}
          </div>
          <p className="mt-1 text-3xl font-semibold text-gray-900">{stats?.pendingCount ?? 0}</p>
        </Card>
        <Card>
          <p className="text-sm text-gray-500">Patients Seen</p>
          <p className="mt-1 text-3xl font-semibold text-gray-900">{summary?.totalPatients ?? 0}</p>
        </Card>
        <Card>
          <p className="text-sm text-gray-500">Avg Sessions / Patient</p>
          <p className="mt-1 text-3xl font-semibold text-gray-900">
            {summary?.averageSessionsPerPatient?.toFixed(1) ?? '0'}
          </p>
        </Card>
      </div>

      {/* Risk distribution */}
      {risk && riskTotal > 0 && (
        <Card>
          <h3 className="mb-3 text-sm font-medium text-gray-700">Risk Distribution</h3>
          <div className="flex h-6 overflow-hidden rounded-full">
            {risk.low > 0 && (
              <div className="bg-green-500" style={{ width: `${(risk.low / riskTotal) * 100}%` }} title={`Low: ${risk.low}`} />
            )}
            {risk.moderate > 0 && (
              <div className="bg-yellow-500" style={{ width: `${(risk.moderate / riskTotal) * 100}%` }} title={`Moderate: ${risk.moderate}`} />
            )}
            {risk.high > 0 && (
              <div className="bg-red-500" style={{ width: `${(risk.high / riskTotal) * 100}%` }} title={`High: ${risk.high}`} />
            )}
            {risk.imminent > 0 && (
              <div className="bg-purple-500" style={{ width: `${(risk.imminent / riskTotal) * 100}%` }} title={`Imminent: ${risk.imminent}`} />
            )}
          </div>
          <div className="mt-2 flex flex-wrap gap-4 text-xs text-gray-600">
            <span className="flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-full bg-green-500" />Low: {risk.low}</span>
            <span className="flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-full bg-yellow-500" />Moderate: {risk.moderate}</span>
            <span className="flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-full bg-red-500" />High: {risk.high}</span>
            <span className="flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-full bg-purple-500" />Imminent: {risk.imminent}</span>
          </div>
        </Card>
      )}

      {/* Flagged patients */}
      <Card>
        <h3 className="mb-4 text-sm font-medium text-gray-700">Flagged Patients</h3>
        {(flaggedPatients.length === 0) ? (
          <p className="text-sm text-gray-400">No flagged patients in this period.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead>
                <tr className="text-left text-xs font-medium uppercase text-gray-500">
                  <th className="pb-2 pr-4">Patient</th>
                  <th className="pb-2 pr-4">Risk Level</th>
                  <th className="pb-2 pr-4">Flagged Sessions</th>
                  <th className="pb-2 pr-4">Reason</th>
                  <th className="pb-2">Last Session</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {flaggedPatients.map((fp) => (
                  <tr key={fp.patientId}>
                    <td className="py-2 pr-4">
                      <Link to={`/review?patient=${fp.patientId}`} className="text-blue-600 hover:underline">
                        {fp.patientIdentifier}
                      </Link>
                    </td>
                    <td className="py-2 pr-4"><RiskBadge level={fp.highestRiskLevel} /></td>
                    <td className="py-2 pr-4">{fp.flaggedSessionCount}</td>
                    <td className="py-2 pr-4 max-w-xs truncate" title={fp.flagReason}>{fp.flagReason}</td>
                    <td className="py-2">{formatDate(fp.lastSessionDate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Per-patient risk trend */}
      <Card>
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-sm font-medium text-gray-700">Patient Risk Trend</h3>
          {flaggedPatients.length > 0 && (
            <select
              value={selectedPatientId}
              onChange={(e) => setSelectedPatientId(e.target.value)}
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              aria-label="Select patient risk trend"
            >
              {flaggedPatients.map((patient) => (
                <option key={patient.patientId} value={patient.patientId}>
                  {patient.patientIdentifier}
                </option>
              ))}
            </select>
          )}
        </div>

        {flaggedPatients.length === 0 ? (
          <p className="text-sm text-gray-400">No flagged patients available for trend analysis.</p>
        ) : riskTrendLoading ? (
          <Spinner />
        ) : riskTrendError ? (
          <div className="rounded-md bg-red-50 p-3 text-sm text-red-700">
            Failed to load risk trend: {(riskTrendError as Error).message}
          </div>
        ) : riskTrend ? (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
              <span className="rounded-full bg-gray-100 px-2 py-1">
                Sessions: {riskTrend.totalSessions}
              </span>
              {riskTrend.latestRiskLevel && (
                <span className="inline-flex items-center gap-1">
                  Latest: <RiskBadge level={riskTrend.latestRiskLevel} />
                </span>
              )}
              {riskTrend.hasEscalation && <Badge variant="warning">Escalation detected</Badge>}
            </div>
            <RiskTrendChart points={riskTrend.points} />
          </div>
        ) : (
          <p className="text-sm text-gray-500">No trend data available.</p>
        )}
      </Card>
    </div>
  )
}
