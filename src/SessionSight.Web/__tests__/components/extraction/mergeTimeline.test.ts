import { describe, it, expect } from 'vitest'
import { buildTimeline } from '../../../src/components/extraction/mergeTimeline'
import type { ExtractionLlmTrace, ExtractionToolCall } from '../../../src/types/extractionSteps'

const makeTrace = (loopRound: number, overrides: Partial<ExtractionLlmTrace> = {}): ExtractionLlmTrace => ({
  modelUsed: 'gpt-4.1-mini',
  loopRound,
  inputTokens: 100,
  outputTokens: 50,
  totalTokens: 150,
  durationMs: 1000,
  promptText: null,
  promptSegmentsJson: JSON.stringify([{ role: 'system', content: 'test' }]),
  responseText: '{"result":"ok"}',
  calledAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

const makeToolCall = (loopRound: number, toolName: string): ExtractionToolCall => ({
  toolName,
  loopRound,
  succeeded: true,
  durationMs: 50,
  calledAt: '2025-01-01T00:00:01Z',
  inputJson: '{}',
  outputJson: '{"ok":true}',
})

describe('buildTimeline', () => {
  it('returns empty array for empty inputs', () => {
    expect(buildTimeline([], [])).toEqual([])
  })

  it('builds timeline from traces and tool calls', () => {
    const traces = [makeTrace(0), makeTrace(1)]
    const toolCalls = [makeToolCall(0, 'check_risk'), makeToolCall(0, 'lookup_code')]

    const timeline = buildTimeline(toolCalls, traces)

    expect(timeline).toHaveLength(2)
    expect(timeline[0].round).toBe(0)
    expect(timeline[0].toolCalls).toHaveLength(2)
    expect(timeline[0].llmTrace).not.toBeNull()
    expect(timeline[1].round).toBe(1)
    expect(timeline[1].toolCalls).toHaveLength(0)
  })

  it('handles tool calls without matching trace', () => {
    const toolCalls = [makeToolCall(0, 'validate')]

    const timeline = buildTimeline(toolCalls, [])

    expect(timeline).toHaveLength(1)
    expect(timeline[0].llmTrace).toBeNull()
    expect(timeline[0].segments).toEqual([])
    expect(timeline[0].toolCalls).toHaveLength(1)
  })

  it('handles traces without matching tool calls', () => {
    const traces = [makeTrace(0)]

    const timeline = buildTimeline([], traces)

    expect(timeline).toHaveLength(1)
    expect(timeline[0].toolCalls).toHaveLength(0)
    expect(timeline[0].responseText).toBe('{"result":"ok"}')
  })

  it('sorts rounds in ascending order', () => {
    const traces = [makeTrace(2), makeTrace(0)]
    const toolCalls = [makeToolCall(1, 'tool_a')]

    const timeline = buildTimeline(toolCalls, traces)

    expect(timeline.map((r) => r.round)).toEqual([0, 1, 2])
  })
})
