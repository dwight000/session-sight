import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { usePatient } from '../hooks/usePatient'
import { usePatientTimeline } from '../hooks/usePatientTimeline'
import { Card } from '../components/ui/Card'
import { Badge } from '../components/ui/Badge'
import { Button } from '../components/ui/Button'
import { Spinner } from '../components/ui/Spinner'
import { RiskBadge } from '../components/ui/RiskBadge'
import type { ReviewStatus } from '../types'

const statusVariant: Record<ReviewStatus, string> = {
  NotFlagged: 'default',
  Pending: 'pending',
  Approved: 'approved',
  Dismissed: 'dismissed',
}

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

function formatModality(modality: string): string {
  switch (modality) {
    case 'InPerson': return 'In-Person'
    case 'TelehealthVideo': return 'Telehealth (Video)'
    case 'TelehealthPhone': return 'Telehealth (Phone)'
    case 'Hybrid': return 'Hybrid'
    default: return modality
  }
}

function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function getDefaultRange() {
  const end = new Date()
  const start = new Date()
  start.setMonth(start.getMonth() - 6)
  return { start: toIsoDate(start), end: toIsoDate(end) }
}

export function PatientTimeline() {
  const { patientId = '' } = useParams<{ patientId: string }>()
  const defaultRange = useMemo(() => getDefaultRange(), [])

  const [startDateInput, setStartDateInput] = useState(defaultRange.start)
  const [endDateInput, setEndDateInput] = useState(defaultRange.end)
  const [appliedStartDate, setAppliedStartDate] = useState(defaultRange.start)
  const [appliedEndDate, setAppliedEndDate] = useState(defaultRange.end)

  const { data: patient, isLoading: patientLoading, error: patientError } = usePatient(patientId)
  const {
    data: timeline,
    isLoading: timelineLoading,
    error: timelineError,
  } = usePatientTimeline(patientId, appliedStartDate, appliedEndDate)

  if (!patientId) {
    return (
      <div className="rounded-md bg-yellow-50 p-4 text-sm text-yellow-800">
        Missing patient id.
      </div>
    )
  }

  if (patientLoading || timelineLoading) return <Spinner />

  const error = patientError || timelineError
  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load patient timeline: {(error as Error).message}
      </div>
    )
  }

  const entries = timeline?.entries ?? []

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/patients" className="text-sm text-blue-600 hover:underline">
          &larr; Back to Patients
        </Link>
        <h2 className="text-2xl font-bold text-gray-900">Patient Timeline</h2>
        {patient && (
          <p className="text-sm text-gray-600">
            {patient.firstName} {patient.lastName} ({patient.externalId})
          </p>
        )}
      </div>

      <Card>
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault()
            setAppliedStartDate(startDateInput)
            setAppliedEndDate(endDateInput)
          }}
        >
          <div>
            <label htmlFor="timeline-start-date" className="block text-sm font-medium text-gray-700">Start Date</label>
            <input
              id="timeline-start-date"
              type="date"
              value={startDateInput}
              onChange={(e) => setStartDateInput(e.target.value)}
              className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div>
            <label htmlFor="timeline-end-date" className="block text-sm font-medium text-gray-700">End Date</label>
            <input
              id="timeline-end-date"
              type="date"
              value={endDateInput}
              onChange={(e) => setEndDateInput(e.target.value)}
              className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <Button type="submit" variant="secondary">Apply Range</Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setStartDateInput(defaultRange.start)
              setEndDateInput(defaultRange.end)
              setAppliedStartDate(defaultRange.start)
              setAppliedEndDate(defaultRange.end)
            }}
          >
            Reset
          </Button>
        </form>
      </Card>

      {timeline && (
        <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
          <span className="rounded-full bg-gray-100 px-2 py-1">Sessions: {timeline.totalSessions}</span>
          {timeline.latestRiskLevel && (
            <span className="inline-flex items-center gap-1">
              Latest Risk: <RiskBadge level={timeline.latestRiskLevel} />
            </span>
          )}
          {timeline.hasEscalation && <Badge variant="warning">Escalation detected</Badge>}
        </div>
      )}

      {entries.length === 0 ? (
        <Card>
          <p className="text-sm text-gray-500">No sessions found in this date range.</p>
        </Card>
      ) : (
        <div className="space-y-3">
          <h3 className="text-sm font-medium text-gray-700">Session Timeline</h3>
          {entries.map((entry) => (
            <Card key={entry.sessionId} className="border-l-4 border-l-blue-500">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-gray-900">
                    Session {entry.sessionNumber} · {entry.sessionType}
                  </p>
                  <p className="text-sm text-gray-600">
                    {formatDate(entry.sessionDate)} · {formatModality(entry.modality)}
                  </p>
                  {entry.daysSincePreviousSession !== null && (
                    <p className="text-xs text-gray-500">
                      {entry.daysSincePreviousSession} days since previous session
                    </p>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {entry.riskLevel ? <RiskBadge level={entry.riskLevel} /> : <Badge>No risk data</Badge>}
                  <Badge variant={statusVariant[entry.reviewStatus as ReviewStatus] ?? 'default'}>
                    {entry.reviewStatus}
                  </Badge>
                  <Link to={`/review/session/${entry.sessionId}`} className="text-sm text-blue-600 hover:underline">
                    Review &rarr;
                  </Link>
                </div>
              </div>

              <div className="mt-4 grid gap-4 text-sm text-gray-700 sm:grid-cols-2 lg:grid-cols-3">
                <div>
                  <p className="text-xs font-medium text-gray-500">Mood</p>
                  <p>Mood score: {entry.moodScore ?? 'N/A'}</p>
                  <p>Change: {entry.moodChange ?? 'N/A'}</p>
                </div>
                <div>
                  <p className="text-xs font-medium text-gray-500">Risk Changes</p>
                  <p>{entry.riskChange ?? 'No risk transition detected'}</p>
                  <p>Mood delta: {entry.moodDelta ?? 'N/A'}</p>
                </div>
                <div>
                  <p className="text-xs font-medium text-gray-500">Document</p>
                  <p>Status: {entry.documentStatus ?? 'None'}</p>
                  {entry.documentFileName && <p className="truncate">{entry.documentFileName}</p>}
                  {entry.documentBlobUri && (
                    <a
                      href={entry.documentBlobUri}
                      target="_blank"
                      rel="noreferrer"
                      className="text-blue-600 hover:underline"
                    >
                      Open source document
                    </a>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
