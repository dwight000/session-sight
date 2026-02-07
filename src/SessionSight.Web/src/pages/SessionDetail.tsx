import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useReviewDetail } from '../hooks/useReviewDetail'
import { useSubmitReview } from '../hooks/useSubmitReview'
import { Card } from '../components/ui/Card'
import { Badge } from '../components/ui/Badge'
import { Button } from '../components/ui/Button'
import { RiskBadge } from '../components/ui/RiskBadge'
import { ConfidenceBar } from '../components/ui/ConfidenceBar'
import { Spinner } from '../components/ui/Spinner'
import type { ReviewStatus, SessionSummary, ExtractedField } from '../types'

const statusVariant: Record<ReviewStatus, string> = {
  NotFlagged: 'default',
  Pending: 'pending',
  Approved: 'approved',
  Dismissed: 'dismissed',
}

const sectionLabels: Record<string, string> = {
  sessionInfo: 'Session Info',
  presentingConcerns: 'Presenting Concerns',
  moodAssessment: 'Mood Assessment',
  riskAssessment: 'Risk Assessment',
  mentalStatusExam: 'Mental Status Exam',
  interventions: 'Interventions',
  diagnoses: 'Diagnoses',
  treatmentProgress: 'Treatment Progress',
  nextSteps: 'Next Steps',
}

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString()
}

function formatFieldValue(val: unknown): string {
  if (val === null || val === undefined) return '\u2014'
  if (Array.isArray(val)) return val.length > 0 ? val.join(', ') : '\u2014'
  if (typeof val === 'object') return JSON.stringify(val)
  return String(val)
}

function formatFieldName(key: string): string {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (s) => s.toUpperCase())
    .trim()
}

function isExtractedField(obj: unknown): obj is ExtractedField {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    'value' in obj &&
    'confidence' in obj
  )
}

function ExtractionSection({
  name,
  data,
}: {
  name: string
  data: Record<string, unknown>
}) {
  const [open, setOpen] = useState(name === 'riskAssessment')

  if (!data || typeof data !== 'object') return null

  const entries = Object.entries(data)

  return (
    <div className="border border-gray-200 rounded-lg">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-medium text-gray-900 hover:bg-gray-50"
      >
        <span>{sectionLabels[name] || name}</span>
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <div className="border-t border-gray-200 px-4 py-3">
          <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            {entries.map(([key, val]) => {
              if (isExtractedField(val)) {
                return (
                  <div key={key} className="rounded-md bg-gray-50 p-3">
                    <p className="text-xs font-medium text-gray-500">{formatFieldName(key)}</p>
                    <p className="mt-1 text-sm text-gray-900">{formatFieldValue(val.value)}</p>
                    <ConfidenceBar value={val.confidence} className="mt-1" />
                    {val.source && (
                      <p className="mt-1 text-xs text-gray-400 italic truncate" title={val.source}>
                        Source: {val.source}
                      </p>
                    )}
                  </div>
                )
              }
              return (
                <div key={key} className="rounded-md bg-gray-50 p-3">
                  <p className="text-xs font-medium text-gray-500">{formatFieldName(key)}</p>
                  <p className="mt-1 text-sm text-gray-900">{formatFieldValue(val)}</p>
                </div>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}

function ReviewActionPanel({ sessionId, currentStatus }: { sessionId: string; currentStatus: ReviewStatus }) {
  const [notes, setNotes] = useState('')
  const [reviewerName, setReviewerName] = useState('')
  const mutation = useSubmitReview(sessionId)

  function handleSubmit(action: 'Approved' | 'Dismissed') {
    if (!reviewerName.trim()) return
    mutation.mutate(
      { action, reviewerName: reviewerName.trim(), notes: notes.trim() || undefined },
    )
  }

  if (currentStatus === 'Approved' || currentStatus === 'Dismissed') {
    return null
  }

  return (
    <Card>
      <h3 className="mb-3 text-sm font-medium text-gray-700">Submit Review</h3>
      {mutation.isSuccess && (
        <div className="mb-3 rounded-md bg-green-50 p-3 text-sm text-green-700">Review submitted.</div>
      )}
      {mutation.isError && (
        <div className="mb-3 rounded-md bg-red-50 p-3 text-sm text-red-700">
          Failed: {(mutation.error as Error).message}
        </div>
      )}
      <div className="space-y-3">
        <div>
          <label className="block text-sm font-medium text-gray-700">Reviewer Name</label>
          <input
            type="text"
            value={reviewerName}
            onChange={(e) => setReviewerName(e.target.value)}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            placeholder="Your name"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700">Notes (optional)</label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            placeholder="Review notes..."
          />
        </div>
        <div className="flex gap-3">
          <Button
            variant="success"
            onClick={() => handleSubmit('Approved')}
            disabled={!reviewerName.trim() || mutation.isPending}
          >
            {mutation.isPending ? 'Submitting...' : 'Approve'}
          </Button>
          <Button
            variant="secondary"
            onClick={() => handleSubmit('Dismissed')}
            disabled={!reviewerName.trim() || mutation.isPending}
          >
            Dismiss
          </Button>
        </div>
      </div>
    </Card>
  )
}

export function SessionDetail() {
  const { sessionId } = useParams<{ sessionId: string }>()
  const { data, isLoading, error } = useReviewDetail(sessionId!)

  if (isLoading) return <Spinner />

  if (error) {
    return (
      <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
        Failed to load session: {(error as Error).message}
      </div>
    )
  }

  if (!data) return null

  let summary: SessionSummary | null = null
  if (data.summaryJson) {
    try {
      summary = JSON.parse(data.summaryJson) as SessionSummary
    } catch {
      // ignore parse error
    }
  }

  const extractionSections = [
    'sessionInfo',
    'presentingConcerns',
    'moodAssessment',
    'riskAssessment',
    'mentalStatusExam',
    'interventions',
    'diagnoses',
    'treatmentProgress',
    'nextSteps',
  ] as const

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center gap-4">
        <Link to="/review" className="text-sm text-blue-600 hover:underline">&larr; Back to Queue</Link>
        <div className="flex-1" />
        <h2 className="text-xl font-bold text-gray-900">{data.patientName}</h2>
        <span className="text-sm text-gray-500">{formatDate(data.sessionDate)}</span>
        <Badge variant={statusVariant[data.reviewStatus]}>{data.reviewStatus}</Badge>
        <span className="text-sm text-gray-500">{Math.round(data.overallConfidence * 100)}% confidence</span>
      </div>

      {/* Summary panel */}
      {summary && (
        <Card>
          <h3 className="mb-2 text-sm font-medium text-gray-700">Session Summary</h3>
          <p className="text-sm text-gray-900">{summary.oneLiner}</p>
          {summary.keyPoints && (
            <div className="mt-3">
              <p className="text-xs font-medium text-gray-500">Key Points</p>
              <p className="mt-1 text-sm text-gray-700">{summary.keyPoints}</p>
            </div>
          )}
          {summary.interventionsUsed && summary.interventionsUsed.length > 0 && (
            <div className="mt-3">
              <p className="text-xs font-medium text-gray-500">Interventions Used</p>
              <div className="mt-1 flex flex-wrap gap-1">
                {summary.interventionsUsed.map((i) => (
                  <Badge key={i}>{i}</Badge>
                ))}
              </div>
            </div>
          )}
          {summary.nextSessionFocus && (
            <div className="mt-3">
              <p className="text-xs font-medium text-gray-500">Next Session Focus</p>
              <p className="mt-1 text-sm text-gray-700">{summary.nextSessionFocus}</p>
            </div>
          )}
          {summary.riskFlags && (
            <div className="mt-3">
              <p className="text-xs font-medium text-gray-500">Risk Flags</p>
              <div className="mt-1 flex items-center gap-2">
                <RiskBadge level={summary.riskFlags.riskLevel} />
                {summary.riskFlags.flags.map((f) => (
                  <span key={f} className="text-sm text-gray-700">{f}</span>
                ))}
              </div>
            </div>
          )}
        </Card>
      )}

      {/* Review reasons */}
      {data.reviewReasons.length > 0 && (
        <Card>
          <h3 className="mb-2 text-sm font-medium text-gray-700">Review Reasons</h3>
          <ul className="list-disc space-y-1 pl-5 text-sm text-gray-700">
            {data.reviewReasons.map((r, i) => (
              <li key={i}>{r}</li>
            ))}
          </ul>
        </Card>
      )}

      {/* Extraction accordion */}
      <div className="space-y-2">
        <h3 className="text-sm font-medium text-gray-700">Clinical Extraction Data</h3>
        {data.data ? (
          extractionSections.map((section) => {
            const sectionData = (data.data as Record<string, Record<string, unknown>>)[section]
            if (!sectionData) return null
            return <ExtractionSection key={section} name={section} data={sectionData} />
          })
        ) : (
          <p className="text-sm text-gray-400">No extraction data available.</p>
        )}
      </div>

      {/* Past reviews */}
      {data.reviews.length > 0 && (
        <Card>
          <h3 className="mb-3 text-sm font-medium text-gray-700">Review History</h3>
          <div className="space-y-2">
            {data.reviews.map((rev) => (
              <div key={rev.id} className="flex items-start gap-3 rounded-md bg-gray-50 p-3">
                <Badge variant={statusVariant[rev.action]}>{rev.action}</Badge>
                <div>
                  <p className="text-sm font-medium text-gray-900">{rev.reviewerName}</p>
                  <p className="text-xs text-gray-500">{formatDateTime(rev.reviewedAt)}</p>
                  {rev.notes && <p className="mt-1 text-sm text-gray-700">{rev.notes}</p>}
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}

      {/* Review action */}
      <ReviewActionPanel sessionId={sessionId!} currentStatus={data.reviewStatus} />
    </div>
  )
}
