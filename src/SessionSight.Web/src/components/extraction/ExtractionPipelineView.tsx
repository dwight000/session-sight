import { useState } from 'react'
import type { ExtractionStep, ExtractionStepName, StepViewMode, ClinicalExtractResult } from '../../types/extractionSteps'
import { useExtractionSteps } from '../../hooks/useExtractionSteps'
import { STEP_ORDER, STEP_DISPLAY_NAMES, estimateCost, formatDurationMs } from './stepConfig'
import { ExtractionStepCard } from './ExtractionStepCard'
import { ViewModeSelector } from './ViewModeSelector'

const GANTT_COLORS: Record<ExtractionStepName, string> = {
  DocumentParse: 'bg-slate-400',
  Intake: 'bg-sky-400',
  ClinicalExtract: 'bg-indigo-500',
  RiskAssess: 'bg-amber-500',
  RiskDebate: 'bg-violet-400',
  Summarize: 'bg-emerald-400',
  SearchIndex: 'bg-gray-400',
}

interface ExtractionPipelineViewProps {
  sessionId: string
  isLive: boolean
}

export function ExtractionPipelineView({ sessionId, isLive }: ExtractionPipelineViewProps) {
  const [viewMode, setViewMode] = useState<StepViewMode>('activity')
  const { data, isLoading, isError } = useExtractionSteps(sessionId, isLive)

  // Historical mode: no data or 404
  if (!isLive && (isError || (!isLoading && !data))) {
    return <p className="text-sm text-gray-400">Processing details not available.</p>
  }

  // Live mode: waiting for pipeline to start
  if (isLive && isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
        Waiting for pipeline to start...
      </div>
    )
  }

  // Build step lookup
  const stepMap = new Map<ExtractionStepName, ExtractionStep>()
  if (data) {
    for (const step of data.steps) {
      stepMap.set(step.stepName, step)
    }
  }

  // Infer current step in live mode: first step not in stepMap
  const completedCount = STEP_ORDER.filter((name) => {
    const step = stepMap.get(name)
    return step && step.status !== 'Running'
  }).length
  const hasFailed = data?.steps.some((s) => s.status === 'Failed') ?? false
  const pipelineCrashed = data?.documentStatus === 'Failed' && !hasFailed
  const currentStepName = isLive && !hasFailed && !pipelineCrashed && completedCount < STEP_ORDER.length
    ? STEP_ORDER[completedCount]
    : null

  // Progress label for live mode — totalSteps includes optional steps (e.g. RiskDebate)
  // already present in the response
  const totalSteps = STEP_ORDER.length + Array.from(stepMap.keys()).filter(n => !STEP_ORDER.includes(n)).length
  const progressLabel = isLive
    ? `${completedCount}/${totalSteps}${currentStepName ? ` \u2014 ${STEP_DISPLAY_NAMES[currentStepName]}` : ''}`
    : null

  // U-1: Max duration for proportional bars
  const maxDurationMs = data ? Math.max(...data.steps.map(s => s.durationMs), 1) : 0

  // B-1: Pipeline summary stats — pipeline complete when all returned steps are terminal
  const pipelineComplete = data && !isLive && data.steps.length >= STEP_ORDER.length
    && data.steps.every(s => s.status === 'Succeeded' || s.status === 'Failed')
  const pipelineStats = pipelineComplete ? (() => {
    const steps = data!.steps
    const totalDuration = steps.reduce((sum, s) => sum + s.durationMs, 0)
    const totalTokens = steps.reduce((sum, s) => sum + s.totalTokens, 0)
    const models = [...new Set(steps.map(s => s.modelUsed).filter(Boolean))]
    const cost = steps.reduce((sum, s) => {
      const c = estimateCost(s.modelUsed, s.inputTokens, s.outputTokens)
      return sum + (c ?? 0)
    }, 0)
    let fieldInfo = ''
    const clinicalStep = steps.find(s => s.stepName === 'ClinicalExtract')
    if (clinicalStep?.resultSummaryJson) {
      try {
        const cr = JSON.parse(clinicalStep.resultSummaryJson) as ClinicalExtractResult
        fieldInfo = ` · ${cr.fieldCount} fields at ${Math.round(cr.overallConfidence * 100)}%`
      } catch { /* skip */ }
    }
    return {
      text: `${steps.length} steps · ${models.length} model${models.length !== 1 ? 's' : ''} · ${formatDurationMs(totalDuration)} · ${totalTokens.toLocaleString()} tokens · $${cost.toFixed(3)}${fieldInfo}`,
      steps,
      totalDuration,
    }
  })() : null

  return (
    <div className="space-y-2">
      {progressLabel && (
        <p className="text-xs font-medium text-blue-700 tabular-nums">{progressLabel}</p>
      )}
      {pipelineCrashed && (
        <div className="rounded-md bg-red-50 border border-red-200 p-3">
          <p className="text-sm font-medium text-red-800">
            Pipeline crashed unexpectedly. Completed steps are shown below.
          </p>
        </div>
      )}
      {data?.documentStatus === 'PartiallyCompleted' && (
        <div className="rounded-md bg-amber-50 border border-amber-200 p-3">
          <p className="text-sm font-medium text-amber-800">
            Extraction partially completed. Some steps did not finish successfully.
          </p>
        </div>
      )}
      <ViewModeSelector value={viewMode} onChange={setViewMode} />

      {/* B-1: Pipeline totals banner + B-1b: Gantt bar */}
      {pipelineStats && (
        <div className="space-y-1.5">
          <div className="text-xs text-gray-500">{pipelineStats.text}</div>
          <div className="flex h-5 rounded-full overflow-hidden bg-gray-100" role="img" aria-label="Pipeline duration chart">
            {pipelineStats.steps.map((s) => {
              const pct = (s.durationMs / pipelineStats.totalDuration) * 100
              return (
                <div
                  key={s.stepName}
                  className={`${GANTT_COLORS[s.stepName]} relative group`}
                  style={{ flexGrow: s.durationMs }}
                  title={`${STEP_DISPLAY_NAMES[s.stepName]}: ${formatDurationMs(s.durationMs)}`}
                >
                  {pct > 15 && (
                    <span className="absolute inset-0 flex items-center justify-center text-[10px] font-medium text-white truncate px-1">
                      {STEP_DISPLAY_NAMES[s.stepName]} {formatDurationMs(s.durationMs)}
                    </span>
                  )}
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Always-visible steps + any optional steps present in this extraction (e.g. RiskDebate) */}
      {[
        ...STEP_ORDER,
        ...Array.from(stepMap.keys()).filter((name) => !STEP_ORDER.includes(name)),
      ]
        .sort((a, b) => {
          const aOrder = stepMap.get(a)?.stepOrder ?? STEP_ORDER.indexOf(a) * 10
          const bOrder = stepMap.get(b)?.stepOrder ?? STEP_ORDER.indexOf(b) * 10
          return aOrder - bOrder
        })
        .map((name) => (
          <ExtractionStepCard
            key={name}
            stepName={name}
            step={stepMap.get(name)}
            isCurrentStep={name === currentStepName}
            defaultExpanded={true}
            showSubSectionsOpen={true}
            sessionId={sessionId}
            viewMode={viewMode}
            maxDurationMs={maxDurationMs}
          />
        ))}
    </div>
  )
}
