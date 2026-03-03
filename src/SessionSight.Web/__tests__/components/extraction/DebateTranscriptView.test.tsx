import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DebateTranscriptView } from '../../../src/components/extraction/DebateTranscriptView'
import type { DebateResultSummary } from '../../../src/types/extractionSteps'

function makeSummary(overrides: Partial<DebateResultSummary> = {}): DebateResultSummary {
  return {
    finalRiskLevel: 'High',
    finalConfidence: 0.82,
    requiresReview: true,
    reviewReasons: [],
    advocateModel: 'gpt-4.1-nano',
    challengerModel: 'Mistral-Large-3',
    judgeModel: 'gpt-4.1-mini',
    rounds: [
      { roundNumber: 1, advocateArgument: 'Advocate opening arg', challengerArgument: 'Challenger opening arg' },
      { roundNumber: 2, advocateArgument: 'Advocate rebuttal', challengerArgument: 'Challenger rebuttal' },
    ],
    judgeSynthesis: 'Risk is elevated based on the evidence presented.',
    ...overrides,
  }
}

describe('DebateTranscriptView', () => {
  it('renders nothing when rounds are empty', () => {
    const { container } = render(
      <DebateTranscriptView summary={makeSummary({ rounds: [] })} />,
    )
    expect(container.innerHTML).toBe('')
  })

  it('shows correct round count in header', () => {
    render(<DebateTranscriptView summary={makeSummary()} />)
    expect(screen.getByText(/Debate Transcript \(2 rounds\)/)).toBeInTheDocument()
  })

  it('shows advocate and challenger labels per round when expanded', async () => {
    const user = userEvent.setup()
    render(<DebateTranscriptView summary={makeSummary()} />)

    await user.click(screen.getByRole('button'))

    const advocateLabels = screen.getAllByText('Advocate')
    expect(advocateLabels).toHaveLength(2)
    const challengerLabels = screen.getAllByText('Challenger')
    expect(challengerLabels).toHaveLength(2)

    expect(screen.getByText('Advocate opening arg')).toBeInTheDocument()
    expect(screen.getByText('Challenger rebuttal')).toBeInTheDocument()
  })

  it('shows judge synthesis text when expanded', async () => {
    const user = userEvent.setup()
    render(<DebateTranscriptView summary={makeSummary()} />)

    await user.click(screen.getByRole('button'))

    expect(screen.getByText('Judge Synthesis')).toBeInTheDocument()
    expect(screen.getByText('Risk is elevated based on the evidence presented.')).toBeInTheDocument()
  })

  it('shows review reasons when present', async () => {
    const user = userEvent.setup()
    render(
      <DebateTranscriptView
        summary={makeSummary({ reviewReasons: ['Conflicting risk signals', 'Low confidence score'] })}
      />,
    )

    await user.click(screen.getByRole('button'))

    expect(screen.getByText('Review Reasons')).toBeInTheDocument()
    expect(screen.getByText('Conflicting risk signals')).toBeInTheDocument()
    expect(screen.getByText('Low confidence score')).toBeInTheDocument()
  })

  it('defaults collapsed and expands on click', async () => {
    const user = userEvent.setup()
    render(<DebateTranscriptView summary={makeSummary()} />)

    // Initially collapsed — no judge synthesis
    expect(screen.queryByText('Judge Synthesis')).not.toBeInTheDocument()

    // Click to expand
    await user.click(screen.getByRole('button'))
    expect(screen.getByText('Judge Synthesis')).toBeInTheDocument()

    // Click to collapse
    await user.click(screen.getByRole('button'))
    expect(screen.queryByText('Judge Synthesis')).not.toBeInTheDocument()
  })
})
