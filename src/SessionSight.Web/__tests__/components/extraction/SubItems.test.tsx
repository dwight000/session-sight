import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ToolCallSection } from '../../../src/components/extraction/ToolCallSection'
import { ToolCallItem } from '../../../src/components/extraction/ToolCallItem'
import { LlmTraceSection } from '../../../src/components/extraction/LlmTraceSection'
import { LlmTraceItem } from '../../../src/components/extraction/LlmTraceItem'
import { mockToolCall, mockLlmTrace } from '../../../src/test/fixtures/extractionSteps'

describe('ToolCallSection', () => {
  it('renders nothing when tool calls empty', () => {
    const { container } = render(<ToolCallSection toolCalls={[]} />)
    expect(container.firstChild).toBeNull()
  })

  it('expands to show tool call items', async () => {
    const user = userEvent.setup()
    render(<ToolCallSection toolCalls={[mockToolCall]} />)

    expect(screen.getByText('Tool Calls (1)')).toBeInTheDocument()
    // Tool call name hidden until section expanded
    expect(screen.queryByText('ExtractMoodTool')).not.toBeInTheDocument()

    await user.click(screen.getByText('Tool Calls (1)'))
    expect(screen.getByText('ExtractMoodTool')).toBeInTheDocument()
  })
})

describe('ToolCallItem', () => {
  it('shows raw I/O on click', async () => {
    const user = userEvent.setup()
    render(<ToolCallItem toolCall={mockToolCall} />)

    expect(screen.queryByText('Input')).not.toBeInTheDocument()
    await user.click(screen.getByText('ExtractMoodTool'))
    expect(screen.getByText('Input')).toBeInTheDocument()
    expect(screen.getByText('{"section":"mood"}')).toBeInTheDocument()
    expect(screen.getByText('Output')).toBeInTheDocument()
    expect(screen.getByText('{"mood":"euthymic"}')).toBeInTheDocument()
  })
})

describe('LlmTraceSection', () => {
  it('renders nothing when traces empty', () => {
    const { container } = render(<LlmTraceSection traces={[]} />)
    expect(container.firstChild).toBeNull()
  })

  it('expands to show trace items', async () => {
    const user = userEvent.setup()
    render(<LlmTraceSection traces={[mockLlmTrace]} />)

    expect(screen.getByText('LLM Traces (1)')).toBeInTheDocument()
    await user.click(screen.getByText('LLM Traces (1)'))
    expect(screen.getByText('150 in / 80 out')).toBeInTheDocument()
  })
})

describe('LlmTraceItem', () => {
  it('shows prompt and response on click', async () => {
    const user = userEvent.setup()
    render(<LlmTraceItem trace={mockLlmTrace} />)

    expect(screen.queryByText('Prompt')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button'))
    expect(screen.getByText('Prompt')).toBeInTheDocument()
    expect(screen.getByText(/Extract clinical fields/)).toBeInTheDocument()
    expect(screen.getByText('Response')).toBeInTheDocument()
  })
})
