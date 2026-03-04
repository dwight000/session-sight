import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ActivityView } from '../../../src/components/extraction/ActivityView'
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
    { role: 'user', content: 'Extract from this therapy note about anxiety treatment' },
  ]),
  responseText: '{"result":"extraction data"}',
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
  outputJson: '{"SuicidalMatches":["suicide"]}',
})

describe('ActivityView', () => {
  it('renders nothing for empty inputs', () => {
    const { container } = render(<ActivityView toolCalls={[]} traces={[]} />)
    expect(container.querySelector('.relative')).toBeNull()
  })

  it('shows system prompt and note events', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)

    expect(screen.getByText('System prompt loaded')).toBeInTheDocument()
    expect(screen.getByText(/Therapy note submitted/)).toBeInTheDocument()
  })

  it('shows LLM call events with 1-based round numbers', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), makeTrace(1)]} />)

    expect(screen.getByText('LLM call (Round 1)')).toBeInTheDocument()
    expect(screen.getByText('LLM call (Round 2)')).toBeInTheDocument()
  })

  it('shows tool call events', () => {
    render(<ActivityView toolCalls={[makeToolCall(0, 'check_risk_keywords')]} traces={[makeTrace(0)]} />)

    expect(screen.getByText('check_risk_keywords')).toBeInTheDocument()
  })

  it('shows Complete footer', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)

    expect(screen.getByText('Complete')).toBeInTheDocument()
  })

  it('shows Processing footer when isStepComplete is false', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} isStepComplete={false} />)

    expect(screen.getByText('Processing...')).toBeInTheDocument()
    expect(screen.queryByText('Complete')).not.toBeInTheDocument()
  })

  it('shows Complete footer when isStepComplete is true', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} isStepComplete={true} />)

    expect(screen.getByText('Complete')).toBeInTheDocument()
    expect(screen.queryByText('Processing...')).not.toBeInTheDocument()
  })

  it('expands details when clicked', async () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)

    await userEvent.click(screen.getByText('System prompt loaded'))

    expect(screen.getByText('You are a clinical assistant')).toBeInTheDocument()
  })

  it('handles duplicate tool names in same round without key collision', () => {
    const tools = [
      makeToolCall(0, 'check_risk_keywords'),
      makeToolCall(0, 'check_risk_keywords'),
    ]
    render(<ActivityView toolCalls={tools} traces={[makeTrace(0)]} />)

    const items = screen.getAllByText('check_risk_keywords')
    expect(items).toHaveLength(2)
  })

  it('LLM call with empty response and no tools is not expandable', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0, { responseText: '' })]} />)

    const llmButton = screen.getByText('LLM call (Round 1)').closest('button')
    expect(llmButton).toBeDisabled()
  })

  it('defaultOpen expands all events, new data inherits expanded state', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} defaultOpen />)

    // System prompt content should be visible without clicking
    expect(screen.getByText('You are a clinical assistant')).toBeInTheDocument()
    // Therapy note content visible in both subtitle and expanded pre
    expect(screen.getAllByText(/Extract from this therapy note/).length).toBeGreaterThanOrEqual(1)
  })

  it('shows tool result summaries for known tool types', () => {
    const tools = [
      { ...makeToolCall(0, 'lookup_diagnosis_code'), outputJson: '{"Code":"F41.1","IsValid":true,"Description":"GAD"}' },
      { ...makeToolCall(0, 'validate_and_score'), outputJson: '{"Errors":[]}' },
      { ...makeToolCall(0, 'validate_and_score'), outputJson: '{"Errors":[{"Field":"mood","Message":"Missing field","Severity":"Error"}]}' },
      { ...makeToolCall(0, 'check_risk_keywords'), outputJson: '{"SuicidalMatches":[]}' },
      { ...makeToolCall(0, 'some_tool'), outputJson: '{"short":"val"}' },
    ]
    render(<ActivityView toolCalls={tools} traces={[makeTrace(0)]} />)

    expect(screen.getByText(/F41.1/)).toBeInTheDocument()
    expect(screen.getByText('Passed')).toBeInTheDocument()
    expect(screen.getByText(/Missing field/)).toBeInTheDocument()
    expect(screen.getByText('No matches')).toBeInTheDocument()
  })

  it('shows tool results returned to AI for multi-round traces', () => {
    const round1Trace = makeTrace(1, {
      promptSegmentsJson: JSON.stringify([
        { role: 'tool', content: '{"result":"tool output"}' },
      ]),
    })
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), round1Trace]} />)

    expect(screen.getByText(/1 tool result returned to AI/)).toBeInTheDocument()
  })

  it('shows failed tool with red dot', () => {
    const tool = { ...makeToolCall(0, 'check_risk_keywords'), succeeded: false }
    const { container } = render(<ActivityView toolCalls={[tool]} traces={[makeTrace(0)]} />)

    // Failed tool dot uses bg-red-500
    const redDot = container.querySelector('.bg-red-500')
    expect(redDot).toBeInTheDocument()
  })

  it('nested tool card expands for long I/O', async () => {
    const longInput = '{"data":"' + 'x'.repeat(200) + '"}'
    const tools = [{ ...makeToolCall(0, 'check_risk'), inputJson: longInput, outputJson: '{"ok":true}' }]
    render(
      <ActivityView
        toolCalls={tools}
        traces={[makeTrace(0, { responseText: '' })]}
      />,
    )

    await userEvent.click(screen.getByText(/AI responded with 1 tool call/))

    // Nested card should show truncated input preview
    const card = screen.getByText('check_risk').closest('button')!
    expect(card).toBeInTheDocument()

    // Click to expand nested card
    await userEvent.click(card)
    expect(screen.getByText('Input:')).toBeInTheDocument()
    expect(screen.getByText('Output:')).toBeInTheDocument()
  })

  it('LLM call with tool calls shows structured details when expanded', async () => {
    render(
      <ActivityView
        toolCalls={[makeToolCall(0, 'check_risk'), makeToolCall(0, 'lookup_code')]}
        traces={[makeTrace(0, { responseText: '' })]}
      />,
    )

    expect(screen.getByText(/AI responded with 2 tool calls/)).toBeInTheDocument()

    await userEvent.click(screen.getByText(/AI responded with 2 tool calls/))

    // Structured tool call cards shown with I/O (nested, no separate dots)
    expect(screen.getByText('check_risk')).toBeInTheDocument()
    expect(screen.getByText('lookup_code')).toBeInTheDocument()
  })

  it('shows plain-language description for system prompt', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)
    expect(screen.getByText('AI role and instructions configured')).toBeInTheDocument()
  })

  it('shows description for LLM round 0', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)
    expect(screen.getByText('AI reading note and generating initial extraction')).toBeInTheDocument()
  })

  it('shows description for LLM round 1+', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), makeTrace(1)]} />)
    expect(screen.getByText('AI refining extraction based on tool feedback')).toBeInTheDocument()
  })

  it('shows tool description for known tool calls', () => {
    render(
      <ActivityView
        toolCalls={[makeToolCall(0, 'check_risk_keywords')]}
        traces={[makeTrace(0, { responseText: '' })]}
      />,
    )
    expect(screen.getByText('Scanning for risk keywords')).toBeInTheDocument()
  })

  it('shows tool results description', () => {
    const round1Trace = makeTrace(1, {
      promptSegmentsJson: JSON.stringify([
        { role: 'tool', content: '{"result":"tool output"}' },
      ]),
    })
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), round1Trace]} />)
    expect(screen.getByText('AI reviewing tool feedback before next round')).toBeInTheDocument()
  })

  it('shows phase labels on LLM events', () => {
    render(
      <ActivityView
        toolCalls={[makeToolCall(0, 'check_risk_keywords')]}
        traces={[makeTrace(0, { responseText: '' }), makeTrace(1)]}
      />,
    )
    expect(screen.getByText('Gathering Data')).toBeInTheDocument()
    expect(screen.getByText('Extraction')).toBeInTheDocument()
  })

  it('shows Refinement phase on later rounds', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), makeTrace(1)]} />)
    expect(screen.getByText('Extraction')).toBeInTheDocument()
    expect(screen.getByText('Refinement')).toBeInTheDocument()
  })

  it('shows Final badge on last round with response', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0), makeTrace(1)]} />)
    expect(screen.getByText('Final')).toBeInTheDocument()
  })

  it('shows no-text-response box for tool-only rounds', () => {
    render(
      <ActivityView
        toolCalls={[makeToolCall(0, 'check_risk_keywords')]}
        traces={[makeTrace(0, { responseText: '' })]}
      />,
    )
    expect(screen.getByText(/No text response/)).toBeInTheDocument()
  })

  it('shows response preview when collapsed', () => {
    render(<ActivityView toolCalls={[]} traces={[makeTrace(0)]} />)
    // defaultOpen is false, so preview should be visible
    expect(screen.getByText(/extraction data/)).toBeInTheDocument()
  })

  it('shows validation callout when previous round had validation errors', () => {
    const validateTool: ExtractionToolCall = {
      ...makeToolCall(0, 'validate_and_score'),
      outputJson: '{"Errors":["Missing mood field","Invalid date format"]}',
    }
    const round1Trace = makeTrace(1, {
      promptSegmentsJson: JSON.stringify([
        { role: 'tool', content: '{"Errors":["Missing mood field"]}' },
      ]),
    })
    render(<ActivityView toolCalls={[validateTool]} traces={[makeTrace(0), round1Trace]} />)
    expect(screen.getByText(/Refined after validation found 2 issues/)).toBeInTheDocument()
  })
})
