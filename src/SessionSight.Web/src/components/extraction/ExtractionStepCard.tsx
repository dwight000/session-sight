import { useState } from 'react'
import type { ExtractionStep, ExtractionStepName, StepViewMode, DocumentParseResult, SearchIndexResult, RiskAssessResult, DebateResultSummary } from '../../types/extractionSteps'
import { STEP_DISPLAY_NAMES, formatDurationMs, formatResultSummary, estimateCost } from './stepConfig'
import { ToolCallSection } from './ToolCallSection'
import { LlmTraceSection } from './LlmTraceSection'
import { ConversationView } from './ConversationView'
import { ActivityView } from './ActivityView'
import { SummaryView } from './SummaryView'
import { DocumentPreview } from './DocumentPreview'
import { ConfidenceHeatmap } from './ConfidenceHeatmap'
import { RiskMergeView } from './RiskMergeView'
import { DebateTranscriptView } from './DebateTranscriptView'
import { useExtractionResult } from '../../hooks/useExtractionResult'

function getRiskBadge(json: string | null): { text: string; color: string } | null {
  if (!json) return null
  try {
    const r = JSON.parse(json) as RiskAssessResult
    if (r.guardrailApplied) return { text: 'Safety guardrail applied', color: 'bg-red-100 text-red-700' }
    if (!r.fieldDecisions?.length) return null
    const changed = r.fieldDecisions.filter(d => d.ruleApplied !== 'no_merge_change').length
    if (changed > 0) return { text: `${changed} field${changed > 1 ? 's' : ''} adjusted`, color: 'bg-amber-100 text-amber-700' }
    return { text: 'All fields verified', color: 'bg-green-100 text-green-700' }
  } catch { return null }
}

interface ExtractionStepCardProps {
  stepName: ExtractionStepName
  step: ExtractionStep | undefined
  isCurrentStep: boolean
  defaultExpanded: boolean
  showSubSectionsOpen?: boolean
  sessionId?: string
  viewMode?: StepViewMode
  maxDurationMs?: number
  pipelineFinished?: boolean
  skippedReason?: string
}

export function ExtractionStepCard({ stepName, step, isCurrentStep, defaultExpanded, showSubSectionsOpen, sessionId, viewMode = 'raw', maxDurationMs, pipelineFinished, skippedReason }: ExtractionStepCardProps) {
  const [open, setOpen] = useState(defaultExpanded)

  const isSkipped = !step && !isCurrentStep && !!skippedReason
  const isPending = !step && !isCurrentStep && !isSkipped
  const isRunning = (isCurrentStep && !step) || step?.status === 'Running'
  const isCompleted = step?.status === 'Succeeded'
  const isFailed = step?.status === 'Failed'

  // Running step with no data yet — show shimmer instead of "0 in / 0 out"
  const isRunningEmpty = isRunning && !!step && step.inputTokens === 0 && step.outputTokens === 0

  const needsExtractionData = (stepName === 'ClinicalExtract' || stepName === 'RiskAssess') && isCompleted && pipelineFinished !== false
  const { data: extractionResult, isLoading: extractionLoading } = useExtractionResult(
    sessionId ?? '',
    open && !!needsExtractionData,
    pipelineFinished ?? true,
  )

  const summary = step ? formatResultSummary(stepName, step.resultSummaryJson) : null

  return (
    <div
      className={[
        'rounded-lg border border-l-4',
        isSkipped ? 'border-dashed border-gray-300 bg-gray-50/50 border-l-gray-300' :
        isRunning ? 'border-blue-300 bg-blue-50/30 border-l-blue-400 animate-pulse-border' :
        isFailed ? 'border-red-300 bg-red-50/30 border-l-red-500' :
        isCompleted ? 'border-gray-200 border-l-green-500' :
        'border-gray-200 border-l-gray-300',
      ].join(' ')}
    >
      {/* Level 0 — always visible header */}
      <button
        onClick={() => setOpen(!open)}
        className={`flex w-full items-center gap-3 px-4 py-3 text-left text-sm hover:bg-gray-50 cursor-pointer ${
          isPending ? 'opacity-60' : ''
        }`}
      >
        {/* Status icon */}
        {isSkipped && <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-dashed border-gray-300 text-gray-400 text-xs">{'\u2014'}</span>}
        {isPending && <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-gray-300" />}
        {isRunning && (
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
        )}
        {isCompleted && (
          <span className="flex h-5 w-5 items-center justify-center rounded-full bg-green-500 text-white text-xs">{'\u2713'}</span>
        )}
        {isFailed && (
          <span className="flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-white text-xs">{'\u2717'}</span>
        )}

        {/* Step name */}
        <span className="font-medium">{STEP_DISPLAY_NAMES[stepName]}</span>

        {/* Skipped reason */}
        {isSkipped && skippedReason && (
          <span className="text-xs text-gray-400 italic">{skippedReason}</span>
        )}

        {/* Duration */}
        {step && step.durationMs > 0 && (
          <span className="text-xs text-gray-400">{formatDurationMs(step.durationMs)}</span>
        )}

        {/* Running indicator */}
        {isRunning && <span className="text-xs text-blue-600">Running...</span>}

        {/* One-liner result summary */}
        {summary && !isFailed && (
          <span className="ml-auto truncate text-xs text-gray-500 max-w-[50%]">{summary}</span>
        )}

        {/* C-3: Risk Assess trust badge */}
        {stepName === 'RiskAssess' && (() => {
          const badge = getRiskBadge(step?.resultSummaryJson ?? null)
          if (!badge) return null
          return <span className={`ml-2 rounded-full px-2 py-0.5 text-[10px] font-medium ${badge.color}`}>{badge.text}</span>
        })()}

        {/* Error on header */}
        {isFailed && step?.errorMessage && (
          <span className="ml-auto truncate text-xs text-red-600 max-w-[50%]">{step.errorMessage}</span>
        )}

        {/* Expand chevron */}
        <span className="ml-auto text-gray-400 flex-shrink-0">{open ? '\u25B2' : '\u25BC'}</span>
      </button>

      {/* U-1: Duration bar proportional to pipeline max */}
      {step && step.durationMs > 0 && maxDurationMs && maxDurationMs > 0 && (
        <div className="h-0.5 bg-gray-100">
          <div
            className={`h-full ${isCompleted ? 'bg-green-400' : isFailed ? 'bg-red-400' : 'bg-blue-400'}`}
            style={{ width: `${Math.max(2, (step.durationMs / maxDurationMs) * 100)}%` }}
          />
        </div>
      )}

      {/* Level 1 — skipped explanation */}
      {open && !step && isSkipped && (
        <div className="border-t border-dashed border-gray-200 px-4 py-3">
          <p className="text-xs text-gray-400 italic">This step was not triggered during extraction.</p>
        </div>
      )}

      {/* Level 1 — placeholder body for pending/running steps without data */}
      {open && !isSkipped && (!step || isRunningEmpty) && (
        <div className="border-t border-gray-200 px-4 py-3">
          <div
            className="grid grid-cols-2 gap-x-6 gap-y-1 rounded p-2 text-xs sm:grid-cols-4"
            style={{
              backgroundImage: 'linear-gradient(90deg, transparent 0%, oklch(0.968 0.007 247.858) 50%, transparent 100%)',
              backgroundSize: '200% 100%',
              animation: 'shimmer 1.8s ease-in-out infinite',
            }}
          >
            <div>
              <span className="text-gray-400">Model:</span>{' '}
              <span className="text-gray-300">--</span>
            </div>
            <div>
              <span className="text-gray-400">Tokens:</span>{' '}
              <span className="text-gray-300">-- / --</span>
            </div>
            <div>
              <span className="text-gray-400">Est. Cost:</span>{' '}
              <span className="text-gray-300">--</span>
            </div>
            <div>
              <span className="text-gray-400">Duration:</span>{' '}
              <span className="text-gray-300">--</span>
            </div>
          </div>
        </div>
      )}

      {/* Level 1 — expanded details with real data */}
      {open && step && !isRunningEmpty && (
        <div className="animate-fade-in border-t border-gray-200 px-4 py-3 space-y-3">
          {/* S-2: Step-aware metadata grid */}
          {step.inputTokens === 0 && step.outputTokens === 0 && step.resultSummaryJson && stepName === 'DocumentParse' ? (() => {
            try {
              const r = JSON.parse(step.resultSummaryJson) as DocumentParseResult
              return (
                <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs sm:grid-cols-5">
                  {step.modelUsed && (
                    <div><span className="text-gray-500">Model:</span>{' '}<span className="font-mono">{step.modelUsed}</span></div>
                  )}
                  <div><span className="text-gray-500">Pages:</span> {r.pageCount}</div>
                  <div><span className="text-gray-500">OCR:</span> {Math.round(r.ocrConfidence * 100)}%</div>
                  <div><span className="text-gray-500">Size:</span> {Math.round(r.fileSizeBytes / 1024)} KB</div>
                  {step.durationMs > 0 && (
                    <div><span className="text-gray-500">Duration:</span> {formatDurationMs(step.durationMs)}</div>
                  )}
                </div>
              )
            } catch { return null }
          })() : step.inputTokens === 0 && step.outputTokens === 0 && step.resultSummaryJson && stepName === 'SearchIndex' ? (() => {
            try {
              const r = JSON.parse(step.resultSummaryJson) as SearchIndexResult
              return (
                <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs sm:grid-cols-3">
                  {step.modelUsed && (
                    <div><span className="text-gray-500">Model:</span>{' '}<span className="font-mono">{step.modelUsed}</span></div>
                  )}
                  <div><span className="text-gray-500">Status:</span> {r.indexed ? 'Indexed' : 'Failed'}</div>
                  {step.durationMs > 0 && (
                    <div><span className="text-gray-500">Duration:</span> {formatDurationMs(step.durationMs)}</div>
                  )}
                </div>
              )
            } catch { return null }
          })() : (
            <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs sm:grid-cols-4">
              {step.modelUsed && (
                <div>
                  <span className="text-gray-500">Model:</span>{' '}
                  <span className="font-mono">{step.modelUsed}</span>
                </div>
              )}
              <div>
                <span className="text-gray-500">Tokens:</span>{' '}
                {step.inputTokens} in / {step.outputTokens} out ({step.totalTokens})
              </div>
              {(() => {
                const cost = estimateCost(step.modelUsed, step.inputTokens, step.outputTokens)
                if (cost === null) return null
                return (
                  <div>
                    <span className="text-gray-500">Est. Cost:</span>{' '}
                    ${cost.toFixed(4)}
                  </div>
                )
              })()}
              {step.durationMs > 0 && (
                <div>
                  <span className="text-gray-500">Duration:</span>{' '}
                  {formatDurationMs(step.durationMs)}
                </div>
              )}
            </div>
          )}

          {/* C-2: Risk keyword callout banner */}
          {stepName === 'RiskAssess' && (() => {
            try {
              const r = JSON.parse(step.resultSummaryJson ?? '') as RiskAssessResult
              if (r.keywordMatches && r.keywordMatches.length > 0) {
                return (
                  <div className="rounded bg-amber-50 border border-amber-200 px-3 py-2 text-xs text-amber-800">
                    <span className="font-medium">Risk keywords detected in note:</span>{' '}
                    <span className="font-semibold">{r.keywordMatches.join(', ')}</span>
                    {' — AI re-verified all risk fields independently'}
                  </div>
                )
              }
            } catch { /* no-op */ }
            return null
          })()}

          {/* Result summary */}
          {summary && (
            <div className="text-xs">
              <span className="text-gray-500">Result:</span>{' '}
              <span className="text-gray-700">{summary}</span>
            </div>
          )}

          {/* Error message */}
          {step.errorMessage && (
            <div className="rounded bg-red-50 p-2 text-xs text-red-700">{step.errorMessage}</div>
          )}

          {/* Document preview (DocumentParse only) */}
          {stepName === 'DocumentParse' && sessionId && (
            <DocumentPreview sessionId={sessionId} defaultOpen={showSubSectionsOpen} />
          )}

          {/* Sub-accordions — view mode dependent */}
          {viewMode === 'raw' && (
            <>
              <ToolCallSection toolCalls={step.toolCalls} defaultOpen={showSubSectionsOpen} />
              <LlmTraceSection traces={step.llmTraces} defaultOpen={showSubSectionsOpen} />
            </>
          )}
          {viewMode === 'conversation' && (
            <ConversationView toolCalls={step.toolCalls} traces={step.llmTraces} defaultOpen={showSubSectionsOpen} />
          )}
          {viewMode === 'activity' && (
            <ActivityView toolCalls={step.toolCalls} traces={step.llmTraces} defaultOpen={showSubSectionsOpen} isStepComplete={isCompleted} />
          )}
          {viewMode === 'summary' && (
            <SummaryView toolCalls={step.toolCalls} traces={step.llmTraces} />
          )}

          {/* Extraction detail panels */}
          {needsExtractionData && extractionLoading && (
            <div className="h-4 w-32 animate-pulse rounded bg-gray-200" />
          )}
          {stepName === 'ClinicalExtract' && extractionResult && (
            <ConfidenceHeatmap data={extractionResult.data} defaultOpen={showSubSectionsOpen} />
          )}
          {stepName === 'RiskAssess' && extractionResult?.riskDiagnostics?.fieldDecisions?.length && (
            <RiskMergeView diagnostics={extractionResult.riskDiagnostics} defaultOpen={showSubSectionsOpen} />
          )}
          {stepName === 'RiskDebate' && step.resultSummaryJson && (() => {
            try {
              const summary: DebateResultSummary = JSON.parse(step.resultSummaryJson)
              return <DebateTranscriptView summary={summary} defaultOpen={showSubSectionsOpen} />
            } catch { return null }
          })()}
        </div>
      )}
    </div>
  )
}
