import { useState } from 'react'
import type { ExtractionStep, ExtractionStepName } from '../../types/extractionSteps'
import { STEP_DISPLAY_NAMES, formatDurationMs, formatResultSummary, estimateCost } from './stepConfig'
import { ToolCallSection } from './ToolCallSection'
import { LlmTraceSection } from './LlmTraceSection'

interface ExtractionStepCardProps {
  stepName: ExtractionStepName
  step: ExtractionStep | undefined
  isCurrentStep: boolean
  defaultExpanded: boolean
}

export function ExtractionStepCard({ stepName, step, isCurrentStep, defaultExpanded }: ExtractionStepCardProps) {
  const [open, setOpen] = useState(defaultExpanded)

  const isPending = !step && !isCurrentStep
  const isRunning = isCurrentStep && (!step || step.status === 'Running')
  const isCompleted = step?.status === 'Succeeded'
  const isFailed = step?.status === 'Failed'

  const summary = step ? formatResultSummary(stepName, step.resultSummaryJson) : null
  const clickable = !!step

  return (
    <div
      className={`rounded-lg border ${
        isRunning ? 'border-blue-300 bg-blue-50/30' :
        isFailed ? 'border-red-300 bg-red-50/30' :
        'border-gray-200'
      }`}
    >
      {/* Level 0 — always visible header */}
      <button
        onClick={() => clickable && setOpen(!open)}
        disabled={!clickable}
        className={`flex w-full items-center gap-3 px-4 py-3 text-left text-sm ${
          clickable ? 'hover:bg-gray-50 cursor-pointer' : 'cursor-default'
        } ${isPending ? 'opacity-50' : ''}`}
      >
        {/* Status icon */}
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

        {/* Error on header */}
        {isFailed && step?.errorMessage && (
          <span className="ml-auto truncate text-xs text-red-600 max-w-[50%]">{step.errorMessage}</span>
        )}

        {/* Expand chevron */}
        {clickable && <span className="ml-auto text-gray-400 flex-shrink-0">{open ? '\u25B2' : '\u25BC'}</span>}
      </button>

      {/* Level 1 — expanded details */}
      {open && step && (
        <div className="border-t border-gray-200 px-4 py-3 space-y-3">
          {/* Metadata grid */}
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

          {/* Sub-accordions */}
          <ToolCallSection toolCalls={step.toolCalls} />
          <LlmTraceSection traces={step.llmTraces} />
        </div>
      )}
    </div>
  )
}
