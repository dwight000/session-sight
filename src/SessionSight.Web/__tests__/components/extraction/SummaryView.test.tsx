import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { SummaryView } from '../../../src/components/extraction/SummaryView'
import type { ExtractionLlmTrace, ExtractionToolCall } from '../../../src/types/extractionSteps'

const makeTrace = (overrides: Partial<ExtractionLlmTrace> = {}): ExtractionLlmTrace => ({
  modelUsed: 'gpt-4.1-mini',
  loopRound: 0,
  inputTokens: 200,
  outputTokens: 100,
  totalTokens: 300,
  durationMs: 5000,
  promptText: null,
  promptSegmentsJson: null,
  responseText: null,
  calledAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

const makeToolCall = (toolName: string): ExtractionToolCall => ({
  toolName,
  loopRound: 0,
  succeeded: true,
  durationMs: 50,
  calledAt: '2025-01-01T00:00:01Z',
  inputJson: '{}',
  outputJson: '{}',
})

describe('SummaryView', () => {
  it('renders nothing for empty arrays', () => {
    const { container } = render(<SummaryView toolCalls={[]} traces={[]} />)
    expect(container.firstChild).toBeNull()
  })

  it('shows round count and model', () => {
    render(<SummaryView toolCalls={[]} traces={[makeTrace(), makeTrace({ loopRound: 1 })]} />)

    expect(screen.getByText(/2 LLM rounds/)).toBeInTheDocument()
    expect(screen.getByText(/gpt-4.1-mini/)).toBeInTheDocument()
  })

  it('shows tool call count and names', () => {
    render(
      <SummaryView
        toolCalls={[makeToolCall('check_risk'), makeToolCall('lookup_code')]}
        traces={[makeTrace()]}
      />,
    )

    expect(screen.getByText(/2 tool calls/)).toBeInTheDocument()
    expect(screen.getByText(/check_risk/)).toBeInTheDocument()
    expect(screen.getByText(/lookup_code/)).toBeInTheDocument()
  })

  it('shows token totals and cost', () => {
    render(<SummaryView toolCalls={[]} traces={[makeTrace({ inputTokens: 1000, outputTokens: 500 })]} />)

    expect(screen.getByText(/1,000 tokens in/)).toBeInTheDocument()
    expect(screen.getByText(/500 out/)).toBeInTheDocument()
    expect(screen.getByText(/Est\./)).toBeInTheDocument()
  })

  it('shows singular LLM round', () => {
    render(<SummaryView toolCalls={[]} traces={[makeTrace()]} />)
    expect(screen.getByText(/1 LLM round using/)).toBeInTheDocument()
  })

  it('shows singular tool call', () => {
    render(<SummaryView toolCalls={[makeToolCall('validate')]} traces={[makeTrace()]} />)
    expect(screen.getByText(/1 tool call:/)).toBeInTheDocument()
  })

  it('handles unknown model with no cost', () => {
    render(<SummaryView toolCalls={[]} traces={[makeTrace({ modelUsed: 'azure-doc-intel' })]} />)
    // Token line should exist but no cost
    expect(screen.getByText(/200 tokens in/)).toBeInTheDocument()
    expect(screen.queryByText(/Est\./)).not.toBeInTheDocument()
  })

  it('handles tool calls with no traces', () => {
    render(<SummaryView toolCalls={[makeToolCall('test')]} traces={[]} />)
    expect(screen.getByText(/1 tool call/)).toBeInTheDocument()
  })

  it('shows total duration', () => {
    render(<SummaryView toolCalls={[]} traces={[makeTrace({ durationMs: 75600 })]} />)

    expect(screen.getByText(/75\.6s/)).toBeInTheDocument()
  })
})
