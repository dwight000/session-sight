import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ConversationView } from '../../../src/components/extraction/ConversationView'
import type { ExtractionLlmTrace, ExtractionToolCall } from '../../../src/types/extractionSteps'

const makeTrace = (loopRound: number, overrides: Partial<ExtractionLlmTrace> = {}): ExtractionLlmTrace => ({
  modelUsed: 'gpt-4.1-mini',
  loopRound,
  inputTokens: 200,
  outputTokens: 100,
  totalTokens: 300,
  durationMs: 5000,
  promptText: null,
  promptSegmentsJson: JSON.stringify([
    { role: 'system', content: 'You are a clinical assistant' },
    { role: 'user', content: 'Extract from therapy note' },
  ]),
  responseText: '{"extraction":"data"}',
  calledAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

const makeToolCall = (loopRound: number, toolName: string): ExtractionToolCall => ({
  toolName,
  loopRound,
  succeeded: true,
  durationMs: 18,
  calledAt: '2025-01-01T00:00:01Z',
  inputJson: '{"query":"risk"}',
  outputJson: '{"result":"ok"}',
})

describe('ConversationView', () => {
  it('renders nothing for empty inputs', () => {
    const { container } = render(<ConversationView toolCalls={[]} traces={[]} />)
    expect(container.firstChild).toBeNull()
  })

  it('shows system prompt header', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)

    expect(screen.getByText('System Prompt')).toBeInTheDocument()
  })

  it('shows therapy note header with word count', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)

    expect(screen.getByText(/Therapy Note \(\d+ words?\)/)).toBeInTheDocument()
  })

  it('shows round dividers', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0), makeTrace(1)]} />)

    expect(screen.getByText(/Round 0/)).toBeInTheDocument()
    expect(screen.getByText(/Round 1/)).toBeInTheDocument()
  })

  it('shows tool calls section when tools are present', () => {
    render(
      <ConversationView
        toolCalls={[makeToolCall(0, 'check_risk'), makeToolCall(0, 'lookup_code')]}
        traces={[makeTrace(0)]}
      />,
    )

    expect(screen.getByText(/AI called 2 tools/)).toBeInTheDocument()
  })

  it('shows AI response block', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)

    expect(screen.getByText(/AI response/)).toBeInTheDocument()
  })

  it('expands system prompt on click', async () => {
    const { default: userEvent } = await import('@testing-library/user-event')
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)

    await userEvent.click(screen.getByText('System Prompt'))
    expect(screen.getByText('You are a clinical assistant')).toBeInTheDocument()
  })

  it('expands AI response on click', async () => {
    const { default: userEvent } = await import('@testing-library/user-event')
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)

    await userEvent.click(screen.getByText(/AI response/))
    expect(screen.getByText('{"extraction":"data"}')).toBeInTheDocument()
  })

  it('marks last round response as final', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} />)
    expect(screen.getByText(/AI response \(final\)/)).toBeInTheDocument()
  })

  it('defaultOpen expands all blocks', () => {
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0)]} defaultOpen />)
    // System prompt content visible without clicking
    expect(screen.getByText('You are a clinical assistant')).toBeInTheDocument()
    // AI response content visible without clicking
    expect(screen.getByText('{"extraction":"data"}')).toBeInTheDocument()
  })

  it('shows tool results for multi-round conversations', () => {
    const round1Trace = makeTrace(1, {
      promptSegmentsJson: JSON.stringify([
        { role: 'tool', content: '{"result":"tool output"}' },
      ]),
    })
    render(<ConversationView toolCalls={[]} traces={[makeTrace(0), round1Trace]} />)
    expect(screen.getByText(/Tool results returned to AI/)).toBeInTheDocument()
  })

  it('works with old-format promptText fallback', () => {
    render(
      <ConversationView
        toolCalls={[]}
        traces={[
          makeTrace(0, {
            promptSegmentsJson: null,
            promptText: '[SYSTEM]\nOld format system prompt\n---\n[USER]\nOld format note',
          }),
        ]}
      />,
    )

    expect(screen.getByText('System Prompt')).toBeInTheDocument()
    expect(screen.getByText(/Therapy Note/)).toBeInTheDocument()
  })
})
