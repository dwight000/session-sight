import type { ExtractionLlmTrace, ExtractionToolCall, PromptSegment } from '../../types/extractionSteps'
import { getSegments } from './promptParser'

export interface TimelineRound {
  round: number
  llmTrace: ExtractionLlmTrace | null
  segments: PromptSegment[]
  toolCalls: ExtractionToolCall[]
  responseText: string | null
  durationMs: number
  inputTokens: number
  outputTokens: number
}

/**
 * Merge tool calls + LLM traces into a unified timeline grouped by loopRound.
 *
 * Chronological order within each round: LLM call first, then tool calls.
 */
export function buildTimeline(
  toolCalls: ExtractionToolCall[],
  traces: ExtractionLlmTrace[],
): TimelineRound[] {
  // Collect unique round numbers from both arrays
  const roundSet = new Set<number>()
  for (const tc of toolCalls) roundSet.add(tc.loopRound)
  for (const tr of traces) roundSet.add(tr.loopRound)

  const rounds = [...roundSet].sort((a, b) => a - b)

  return rounds.map((round) => {
    const trace = traces.find((t) => t.loopRound === round) ?? null
    const roundToolCalls = toolCalls.filter((tc) => tc.loopRound === round)
    const segments = trace ? getSegments(trace) : []

    return {
      round,
      llmTrace: trace,
      segments,
      toolCalls: roundToolCalls,
      responseText: trace?.responseText ?? null,
      durationMs: trace?.durationMs ?? 0,
      inputTokens: trace?.inputTokens ?? 0,
      outputTokens: trace?.outputTokens ?? 0,
    }
  })
}
