import type { ExtractionStep, ExtractionStepName } from '../../types/extractionSteps'
import { useExtractionSteps } from '../../hooks/useExtractionSteps'
import { STEP_ORDER } from './stepConfig'
import { ExtractionStepCard } from './ExtractionStepCard'

interface ExtractionPipelineViewProps {
  sessionId: string
  isLive: boolean
}

export function ExtractionPipelineView({ sessionId, isLive }: ExtractionPipelineViewProps) {
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
  const currentStepName = isLive && !hasFailed && completedCount < STEP_ORDER.length
    ? STEP_ORDER[completedCount]
    : null

  return (
    <div className="space-y-2">
      {STEP_ORDER.map((name) => (
        <ExtractionStepCard
          key={name}
          stepName={name}
          step={stepMap.get(name)}
          isCurrentStep={name === currentStepName}
          defaultExpanded={!isLive}
        />
      ))}
    </div>
  )
}
