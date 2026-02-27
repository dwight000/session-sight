import type { ExtractionToolCall, ExtractionLlmTrace } from '../../types/extractionSteps'
import { estimateCost, formatDurationMs } from './stepConfig'

interface SummaryViewProps {
  toolCalls: ExtractionToolCall[]
  traces: ExtractionLlmTrace[]
}

export function SummaryView({ toolCalls, traces }: SummaryViewProps) {
  if (traces.length === 0 && toolCalls.length === 0) return null

  const roundCount = traces.length
  const models = [...new Set(traces.map((t) => t.modelUsed))].join(', ')
  const uniqueTools = [...new Set(toolCalls.map((tc) => tc.toolName))]
  const totalIn = traces.reduce((sum, t) => sum + t.inputTokens, 0)
  const totalOut = traces.reduce((sum, t) => sum + t.outputTokens, 0)
  const totalDurationMs = traces.reduce((sum, t) => sum + t.durationMs, 0)

  // Compute cost from first model (all traces in a step typically use same model)
  const model = traces[0]?.modelUsed ?? ''
  const cost = estimateCost(model, totalIn, totalOut)

  return (
    <div className="rounded bg-gray-50 p-3 text-xs text-gray-600 space-y-1">
      {roundCount > 0 && (
        <div>
          {roundCount} LLM round{roundCount !== 1 ? 's' : ''}{models ? ` using ${models}` : ''}
        </div>
      )}
      {toolCalls.length > 0 && (
        <div>
          {toolCalls.length} tool call{toolCalls.length !== 1 ? 's' : ''}:{' '}
          {uniqueTools.join(', ')}
        </div>
      )}
      {(totalIn > 0 || totalOut > 0) && (
        <div>
          {totalIn.toLocaleString()} tokens in / {totalOut.toLocaleString()} out
          {cost !== null ? ` \u00B7 Est. $${cost.toFixed(4)}` : ''}
        </div>
      )}
      {totalDurationMs > 0 && <div>Total duration: {formatDurationMs(totalDurationMs)}</div>}
    </div>
  )
}
