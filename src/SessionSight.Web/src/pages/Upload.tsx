import { useState, useRef, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useSessions } from '../hooks/useSessions'
import { usePatients } from '../hooks/usePatients'
import { useUploadDocument } from '../hooks/useUploadDocument'
import { useExtractionSteps } from '../hooks/useExtractionSteps'
import { Button } from '../components/ui/Button'
import { Spinner } from '../components/ui/Spinner'
import { ExtractionPipelineView } from '../components/extraction/ExtractionPipelineView'

interface SampleDoc {
  id: string
  filename: string
  title: string
  description: string
  previewText: string
  riskTier: 'low' | 'moderate' | 'high'
  diagnosis: string
  metaStrip: string
  keyExtractions: { field: string; value: string }[]
  clinicalNote: string
}

function formatDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString()
}

type DocSource = 'sample' | 'own'

export function Upload() {
  const { data: sessions, isLoading: sessionsLoading } = useSessions({ hasDocument: false })
  const { data: patients } = usePatients()
  const uploadDocument = useUploadDocument()
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const pipelineRef = useRef<HTMLDivElement>(null)

  const [selectedSessionId, setSelectedSessionId] = useState<string>('')
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [docSource, setDocSource] = useState<DocSource>('sample')
  const [samples, setSamples] = useState<SampleDoc[]>([])
  const [expandedSample, setExpandedSample] = useState<string | null>(null)
  const [loadingSample, setLoadingSample] = useState<string | null>(null)
  const [processingSessionId, setProcessingSessionId] = useState<string | null>(null)
  const [selectedSample, setSelectedSample] = useState<SampleDoc | null>(null)

  // Poll extraction steps for the session being processed — drives status banners
  const { data: pipelineData } = useExtractionSteps(processingSessionId ?? '', !!processingSessionId)
  const pipelineStatus = pipelineData?.documentStatus

  useEffect(() => {
    fetch('/samples/samples.json')
      .then((r) => r.json())
      .then((data: SampleDoc[]) => setSamples(data))
      .catch(() => setSamples([]))
  }, [])

  // Scroll pipeline into view when it appears
  useEffect(() => {
    if (processingSessionId) {
      pipelineRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [processingSessionId])

  // Build patient name lookup
  const patientNames = new Map<string, string>()
  patients?.forEach((p) => {
    patientNames.set(p.id, `${p.firstName} ${p.lastName}`)
  })

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) {
      setSelectedFile(file)
      setUploadError(null)
    }
  }

  async function handleUseSample(sample: SampleDoc) {
    setLoadingSample(sample.id)
    try {
      const response = await fetch(`/samples/${sample.filename}`)
      const blob = await response.blob()
      const file = new File([blob], sample.filename, { type: 'application/pdf' })
      setSelectedFile(file)
      setSelectedSample(sample)
      setUploadError(null)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }
    } catch {
      setUploadError(`Failed to load sample: ${sample.title}`)
    } finally {
      setLoadingSample(null)
    }
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedSessionId || !selectedFile) return

    setUploadError(null)
    setProcessingSessionId(selectedSessionId)
    uploadDocument.mutate(
      { sessionId: selectedSessionId, file: selectedFile },
      {
        onSuccess: () => {
          // 202 accepted — pipeline view takes over for progress. Clear form.
          queryClient.invalidateQueries({ queryKey: ['extractionSteps', selectedSessionId] })
          queryClient.invalidateQueries({ queryKey: ['extractionResult', selectedSessionId] })
          queryClient.invalidateQueries({ queryKey: ['reviewDetail', selectedSessionId] })
          setSelectedSessionId('')
          setSelectedFile(null)
          if (fileInputRef.current) {
            fileInputRef.current.value = ''
          }
        },
        onError: (error) => {
          queryClient.invalidateQueries({ queryKey: ['extractionSteps', selectedSessionId] })
          queryClient.invalidateQueries({ queryKey: ['extractionResult', selectedSessionId] })
          queryClient.invalidateQueries({ queryKey: ['reviewDetail', selectedSessionId] })
          setUploadError((error as Error).message)
        },
      }
    )
  }

  if (sessionsLoading) return <Spinner />

  const availableSessions = sessions?.filter((s) => !s.hasDocument) || []

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-gray-900">Upload Session Note</h2>
        <p className="mt-1 text-sm text-gray-500">
          Upload a therapy note document for processing. The note will be analyzed and extracted automatically.
        </p>
      </div>

      {/* Expected outcome card — stays visible during extraction for comparison */}
      {processingSessionId && selectedSample && (
        <div className="rounded-md border border-gray-200 bg-white p-4">
          <div className="flex items-center gap-2">
            <span className="text-xs font-medium text-gray-500 uppercase tracking-wide">Expected Outcome</span>
            <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${
              selectedSample.riskTier === 'high' ? 'bg-red-100 text-red-700' :
              selectedSample.riskTier === 'moderate' ? 'bg-amber-100 text-amber-700' :
              'bg-gray-100 text-gray-600'
            }`}>
              {selectedSample.riskTier === 'high' ? 'High Risk' :
               selectedSample.riskTier === 'moderate' ? 'Moderate Risk' : 'Low Risk'}
            </span>
            <span className="text-sm font-medium text-gray-900">{selectedSample.title}</span>
          </div>
          <p className="mt-1 text-xs text-gray-500">{selectedSample.metaStrip}</p>
          <div className="mt-2 bg-gray-50 rounded p-2 space-y-1">
            {selectedSample.keyExtractions.map((ext) => (
              <div key={ext.field} className="flex text-xs gap-2">
                <span className="text-gray-500 font-medium shrink-0 w-28">{ext.field}</span>
                <span className="text-gray-700">{ext.value}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Pipeline view — above the form when active */}
      {processingSessionId && (
        <div ref={pipelineRef} className="rounded-md border border-blue-200 bg-blue-50/30 p-4">
          <p className="mb-3 text-sm font-medium text-blue-800">Extraction Pipeline</p>
          <ExtractionPipelineView sessionId={processingSessionId} isLive={true} />
        </div>
      )}

      {pipelineStatus === 'Completed' && (
        <div className="rounded-md bg-green-50 p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-green-800">
                Extraction completed successfully!
              </p>
              <div className="mt-2">
                <Link
                  to={`/review/session/${processingSessionId}`}
                  className="text-sm font-medium text-green-700 underline hover:text-green-600"
                >
                  View extraction results
                </Link>
              </div>
            </div>
          </div>
        </div>
      )}

      {pipelineStatus === 'PartiallyCompleted' && (
        <div className="rounded-md bg-amber-50 p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-amber-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-amber-800">
                Extraction partially completed — some steps need retry.
              </p>
              <div className="mt-2">
                <Link
                  to={`/review/session/${processingSessionId}`}
                  className="text-sm font-medium text-amber-700 underline hover:text-amber-600"
                >
                  View extraction results
                </Link>
              </div>
            </div>
          </div>
        </div>
      )}

      {pipelineStatus === 'Failed' && (
        <div className="rounded-md bg-red-50 p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-red-800">
                Extraction failed. You can retry from the Sessions page.
              </p>
            </div>
          </div>
        </div>
      )}

      {uploadError && (
        <div className="rounded-md bg-red-50 p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-red-800">
                {uploadError}
              </p>
            </div>
          </div>
        </div>
      )}

      {availableSessions.length === 0 ? (
        <div className="rounded-md bg-yellow-50 p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-yellow-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm font-medium text-yellow-800">
                No sessions available for upload. All sessions already have documents, or you need to create a session first.
              </p>
              <div className="mt-2">
                <Link
                  to="/sessions"
                  className="text-sm font-medium text-yellow-700 underline hover:text-yellow-600"
                >
                  Go to Sessions
                </Link>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="rounded-lg border border-gray-200 bg-white p-6 space-y-6">
          <div>
            <label htmlFor="session-select" className="block text-sm font-medium text-gray-700">Select Session</label>
            <select
              id="session-select"
              required
              value={selectedSessionId}
              onChange={(e) => setSelectedSessionId(e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              <option value="">Select a session...</option>
              {availableSessions.map((session) => (
                <option key={session.id} value={session.id}>
                  {patientNames.get(session.patientId) || 'Unknown'} - {formatDate(session.sessionDate)} - {session.sessionType}
                </option>
              ))}
            </select>
            <p className="mt-1 text-xs text-gray-500">
              Only sessions without documents are shown.
            </p>
          </div>

          {/* Document source tabs */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Document Source</label>
            <div className="flex rounded-md border border-gray-300 w-fit" role="tablist">
              <button
                type="button"
                role="tab"
                aria-selected={docSource === 'sample'}
                onClick={() => setDocSource('sample')}
                className={`px-4 py-1.5 text-sm font-medium rounded-l-md ${
                  docSource === 'sample'
                    ? 'bg-blue-50 text-blue-700 border-r border-gray-300'
                    : 'text-gray-500 hover:text-gray-700 border-r border-gray-300'
                }`}
              >
                Sample Documents
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={docSource === 'own'}
                onClick={() => { setDocSource('own'); setSelectedSample(null) }}
                className={`px-4 py-1.5 text-sm font-medium rounded-r-md ${
                  docSource === 'own'
                    ? 'bg-blue-50 text-blue-700'
                    : 'text-gray-500 hover:text-gray-700'
                }`}
              >
                Your Document
              </button>
            </div>
          </div>

          {docSource === 'sample' && (
            <div className="space-y-3">
              {[...samples]
                .sort((a, b) => {
                  const order: Record<string, number> = { high: 0, moderate: 1, low: 2 }
                  return (order[a.riskTier] ?? 2) - (order[b.riskTier] ?? 2)
                })
                .map((sample) => {
                  const badgeStyles: Record<string, string> = {
                    high: 'bg-red-100 text-red-700',
                    moderate: 'bg-amber-100 text-amber-700',
                    low: 'bg-gray-100 text-gray-600',
                  }
                  const badgeLabel: Record<string, string> = {
                    high: 'High Risk',
                    moderate: 'Moderate Risk',
                    low: 'Low Risk',
                  }
                  return (
                    <div
                      key={sample.id}
                      className="rounded-md border border-gray-200 p-4 hover:border-blue-300 transition-colors"
                    >
                      {/* Header row */}
                      <div className="flex items-center gap-2">
                        <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${badgeStyles[sample.riskTier] ?? badgeStyles.low}`}>
                          {badgeLabel[sample.riskTier] ?? 'Low Risk'}
                        </span>
                        <span className="text-sm font-medium text-gray-900 flex-1">{sample.title}</span>
                        <button
                          type="button"
                          onClick={() => handleUseSample(sample)}
                          disabled={loadingSample === sample.id}
                          className="text-xs font-medium text-blue-600 hover:text-blue-800 underline whitespace-nowrap"
                        >
                          {loadingSample === sample.id ? 'Loading...' : 'Use This'}
                        </button>
                      </div>

                      {/* Meta strip */}
                      <p className="mt-1 text-xs text-gray-500">{sample.metaStrip}</p>

                      {/* Key extractions */}
                      <div className="mt-2 bg-gray-50 rounded p-2 space-y-1">
                        {sample.keyExtractions.map((ext) => (
                          <div key={ext.field} className="flex text-xs gap-2">
                            <span className="text-gray-500 font-medium shrink-0 w-28">{ext.field}</span>
                            <span className="text-gray-700">{ext.value}</span>
                          </div>
                        ))}
                      </div>

                      {/* Footer: Why this sample + Open PDF */}
                      <div className="mt-2 flex items-start justify-between gap-2">
                        <div>
                          <button
                            type="button"
                            onClick={() => setExpandedSample(expandedSample === sample.id ? null : sample.id)}
                            className="text-xs text-gray-400 hover:text-gray-600"
                          >
                            {expandedSample === sample.id ? '▾ Why this sample' : '▸ Why this sample'}
                          </button>
                          {expandedSample === sample.id && (
                            <p className="mt-1 text-xs text-gray-400 italic">{sample.clinicalNote}</p>
                          )}
                        </div>
                        <a
                          href={`/samples/${sample.filename}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-xs text-gray-400 hover:text-gray-600 whitespace-nowrap"
                        >
                          Open PDF ↗
                        </a>
                      </div>
                    </div>
                  )
                })}
            </div>
          )}

          {docSource === 'own' && (
            <div>
              <label htmlFor="document-file" className="block text-sm font-medium text-gray-700">Document File</label>
              <input
                id="document-file"
                ref={fileInputRef}
                type="file"
                accept=".pdf,.docx,.doc,.jpg,.jpeg,.png,.tiff,.bmp"
                onChange={handleFileChange}
                className="mt-1 block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-medium file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
              />
              <p className="mt-1 text-xs text-gray-500">
                Supported formats: PDF, DOCX, DOC, JPG, PNG, TIFF, BMP
              </p>
            </div>
          )}

          {selectedFile && (
            <div className="rounded-md bg-gray-50 p-3">
              <p className="text-sm text-gray-700">
                <span className="font-medium">Selected file:</span> {selectedFile.name} ({(selectedFile.size / 1024).toFixed(1)} KB)
              </p>
            </div>
          )}

          <div className="flex justify-end">
            <Button
              type="submit"
              disabled={!selectedSessionId || !selectedFile || uploadDocument.isPending}
            >
              {uploadDocument.isPending ? (
                <span className="flex items-center gap-2">
                  <Spinner className="h-4 w-4" />
                  Processing...
                </span>
              ) : (
                'Upload & Extract'
              )}
            </Button>
          </div>

        </form>
      )}
    </div>
  )
}
