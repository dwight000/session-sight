import { useState, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { Card } from '../components/ui/Card'
import { ConfidenceBar } from '../components/ui/ConfidenceBar'
import { usePatients } from '../hooks/usePatients'
import { useSessions } from '../hooks/useSessions'
import { useAskQuestion } from '../hooks/useAskQuestion'
import type { QAResponse } from '../types'

interface QAEntry {
  question: string
  response: QAResponse
}

export function QA() {
  const [selectedPatientId, setSelectedPatientId] = useState('')
  const [question, setQuestion] = useState('')
  const [entries, setEntries] = useState<QAEntry[]>([])
  const [error, setError] = useState<string | null>(null)

  const { data: patients } = usePatients()
  const { data: patientSessions } = useSessions(
    selectedPatientId ? { patientId: selectedPatientId } : undefined
  )
  const mutation = useAskQuestion(selectedPatientId)

  const hasPartialSessions = patientSessions?.some(
    (s) => s.documentStatus === 'PartiallyCompleted'
  ) ?? false

  const handlePatientChange = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedPatientId(e.target.value)
    setEntries([])
    setError(null)
  }, [])

  const handleSubmit = useCallback((e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedPatientId || !question.trim()) return

    const currentQuestion = question
    setQuestion('')
    setError(null)

    mutation.mutate(
      { question: currentQuestion },
      {
        onSuccess: (response) => {
          setEntries((prev) => [...prev, { question: currentQuestion, response }])
        },
        onError: (err) => {
          setError(err instanceof Error ? err.message : 'An unexpected error occurred')
        },
      },
    )
  }, [selectedPatientId, question, mutation])

  const canSubmit = selectedPatientId && question.trim() && !mutation.isPending

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Q&A</h2>
        <p className="mt-1 text-sm text-gray-500">
          Ask natural language questions about patient session histories.
        </p>
      </div>

      <Card>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="patient-select" className="block text-sm font-medium text-gray-700">
              Patient
            </label>
            <select
              id="patient-select"
              value={selectedPatientId}
              onChange={handlePatientChange}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              <option value="">Select a patient...</option>
              {patients?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.firstName} {p.lastName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="question-input" className="block text-sm font-medium text-gray-700">
              Question
            </label>
            <textarea
              id="question-input"
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              maxLength={2000}
              rows={3}
              placeholder="e.g., What were the main concerns discussed in recent sessions?"
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <button
            type="submit"
            disabled={!canSubmit}
            className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {mutation.isPending && (
              <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            )}
            {mutation.isPending ? 'Thinking...' : 'Ask'}
          </button>

          {mutation.isPending && (
            <p className="text-sm text-gray-500">
              Processing with AI — this may take 5–30 seconds.
            </p>
          )}
        </form>
      </Card>

      {hasPartialSessions && (
        <div className="rounded-md border border-amber-200 bg-amber-50 p-4">
          <p className="text-sm text-amber-800">
            Some sessions may be missing from search results due to indexing issues.
          </p>
        </div>
      )}

      {error && (
        <div className="rounded-md border border-red-200 bg-red-50 p-4">
          <p className="text-sm text-red-800">{error}</p>
        </div>
      )}

      <div className="space-y-4">
        {entries.map((entry, i) => (
          <Card key={i}>
            <div className="space-y-3">
              <div>
                <p className="text-xs font-medium uppercase text-gray-400">Question</p>
                <p className="text-sm text-gray-900">{entry.question}</p>
              </div>

              <div>
                <p className="text-xs font-medium uppercase text-gray-400">Answer</p>
                <p className="whitespace-pre-wrap text-sm text-gray-900">{entry.response.answer}</p>
              </div>

              {entry.response.warning && (
                <div className="rounded-md border border-amber-200 bg-amber-50 p-3">
                  <p className="text-sm text-amber-800">{entry.response.warning}</p>
                </div>
              )}

              <div className="flex flex-wrap items-center gap-3">
                <ConfidenceBar value={entry.response.confidence} />
                <span className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-800">
                  {entry.response.modelUsed}
                </span>
                {entry.response.toolCallCount > 0 && (
                  <span className="inline-flex items-center rounded-full bg-purple-100 px-2.5 py-0.5 text-xs font-medium text-purple-800">
                    Agentic ({entry.response.toolCallCount} tool calls)
                  </span>
                )}
              </div>

              {entry.response.sources.length > 0 && (
                <div>
                  <p className="mb-2 text-xs font-medium uppercase text-gray-400">Sources</p>
                  <div className="space-y-2">
                    {entry.response.sources.map((source, j) => (
                      <div
                        key={j}
                        className="flex items-start justify-between rounded-md border border-gray-100 bg-gray-50 p-3"
                      >
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            {source.sessionDate && (
                              <span className="text-xs text-gray-500">
                                {new Date(source.sessionDate).toLocaleDateString()}
                              </span>
                            )}
                            {source.sessionType && (
                              <span className="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800">
                                {source.sessionType}
                              </span>
                            )}
                            {source.relevanceScore > 0 && (
                              <span className="text-xs text-gray-400">
                                {Math.round(source.relevanceScore * 100)}% relevant
                              </span>
                            )}
                          </div>
                          {source.summary && (
                            <p className="mt-1 truncate text-xs text-gray-600">
                              {source.summary}
                            </p>
                          )}
                        </div>
                        <Link
                          to={`/review/session/${source.sessionId}`}
                          className="ml-3 shrink-0 text-xs font-medium text-blue-600 hover:text-blue-800"
                        >
                          View session →
                        </Link>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}
