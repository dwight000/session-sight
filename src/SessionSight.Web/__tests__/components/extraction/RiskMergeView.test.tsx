import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RiskMergeView } from '../../../src/components/extraction/RiskMergeView'
import { mockExtractionResult } from '../../../src/test/fixtures/extractionSteps'

describe('RiskMergeView', () => {
  const diagnostics = mockExtractionResult.riskDiagnostics!

  it('returns null when diagnostics is null', () => {
    const { container } = render(<RiskMergeView diagnostics={null} />)
    expect(container.innerHTML).toBe('')
  })

  it('returns null when fieldDecisions is empty', () => {
    const { container } = render(
      <RiskMergeView diagnostics={{ ...diagnostics, fieldDecisions: [] }} />,
    )
    expect(container.innerHTML).toBe('')
  })

  it('shows field count in toggle button', () => {
    render(<RiskMergeView diagnostics={diagnostics} />)
    expect(screen.getByText('Risk Merge (2 fields)')).toBeInTheDocument()
  })

  it('renders field decisions when open', () => {
    render(<RiskMergeView diagnostics={diagnostics} defaultOpen={true} />)

    // Field names
    expect(screen.getByText('Suicidal Ideation')).toBeInTheDocument()
    expect(screen.getByText('Overall Risk Level')).toBeInTheDocument()

    // Rule badges
    const badges = screen.getAllByText('ConservativeMerge')
    expect(badges).toHaveLength(2)
  })

  it('renders three-column values for each decision', () => {
    render(<RiskMergeView diagnostics={diagnostics} defaultOpen={true} />)

    // First decision: suicidalIdeation
    expect(screen.getByText('None')).toBeInTheDocument() // original
    const passiveTexts = screen.getAllByText('Passive')
    expect(passiveTexts.length).toBeGreaterThanOrEqual(2) // re-extracted + final

    // Column headers
    const originals = screen.getAllByText('Original')
    expect(originals).toHaveLength(2) // one per decision
  })

  it('renders criteria tags', () => {
    render(<RiskMergeView diagnostics={diagnostics} defaultOpen={true} />)
    // 'Higher severity wins' appears in both decisions
    const severityTags = screen.getAllByText('Higher severity wins')
    expect(severityTags.length).toBe(2)
    expect(screen.getByText('Source text mentions passive SI')).toBeInTheDocument()
  })

  it('renders reasoning text', () => {
    render(<RiskMergeView diagnostics={diagnostics} defaultOpen={true} />)
    expect(screen.getByText(/Re-extraction found passive SI/)).toBeInTheDocument()
  })

  it('shows guardrail banner when applied', () => {
    render(<RiskMergeView diagnostics={diagnostics} defaultOpen={true} />)
    expect(screen.getByText('Guardrail Applied')).toBeInTheDocument()
    expect(screen.getByText(/Self-harm:/)).toBeInTheDocument()
  })

  it('hides guardrail banner when not applied', () => {
    render(
      <RiskMergeView
        diagnostics={{ ...diagnostics, guardrailApplied: false }}
        defaultOpen={true}
      />,
    )
    expect(screen.queryByText('Guardrail Applied')).not.toBeInTheDocument()
  })

  it('starts closed by default, opens on click', async () => {
    const user = userEvent.setup()
    render(<RiskMergeView diagnostics={diagnostics} />)

    // Content not visible initially
    expect(screen.queryByText('Suicidal Ideation')).not.toBeInTheDocument()

    await user.click(screen.getByText('Risk Merge (2 fields)'))
    expect(screen.getByText('Suicidal Ideation')).toBeInTheDocument()
  })
})
