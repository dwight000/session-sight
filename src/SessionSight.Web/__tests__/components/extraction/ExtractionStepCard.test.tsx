import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ExtractionStepCard } from '../../../src/components/extraction/ExtractionStepCard'
import { makeStep } from '../../../src/test/fixtures/extractionSteps'

describe('ExtractionStepCard', () => {
  it('renders pending state with grayed-out name', () => {
    render(
      <ExtractionStepCard stepName="DocumentParse" step={undefined} isCurrentStep={false} defaultExpanded={false} />,
    )
    expect(screen.getByText('Document Parse')).toBeInTheDocument()
    // Button should be disabled for pending
    const btn = screen.getByRole('button')
    expect(btn).toBeDisabled()
  })

  it('renders running state with spinner text', () => {
    render(
      <ExtractionStepCard stepName="Intake" step={undefined} isCurrentStep={true} defaultExpanded={false} />,
    )
    expect(screen.getByText('Running...')).toBeInTheDocument()
  })

  it('renders completed step with duration', () => {
    const step = makeStep('DocumentParse', 0, { durationMs: 1200 })
    render(
      <ExtractionStepCard stepName="DocumentParse" step={step} isCurrentStep={false} defaultExpanded={false} />,
    )
    expect(screen.getByText('1.2s')).toBeInTheDocument()
  })

  it('expands and collapses on click', async () => {
    const user = userEvent.setup()
    const step = makeStep('ClinicalExtract', 2, {
      inputTokens: 2000,
      outputTokens: 800,
      totalTokens: 2800,
    })
    render(
      <ExtractionStepCard stepName="ClinicalExtract" step={step} isCurrentStep={false} defaultExpanded={false} />,
    )

    // Initially collapsed — no token details
    expect(screen.queryByText(/Tokens:/)).not.toBeInTheDocument()

    // Click to expand
    await user.click(screen.getByRole('button'))
    expect(screen.getByText(/Tokens:/)).toBeInTheDocument()

    // Click to collapse
    await user.click(screen.getByRole('button', { name: /Clinical Extract/i }))
    expect(screen.queryByText(/Tokens:/)).not.toBeInTheDocument()
  })

  it('renders failed step with error message', () => {
    const step = makeStep('ClinicalExtract', 2, {
      status: 'Failed',
      errorMessage: 'Rate limit exceeded',
    })
    render(
      <ExtractionStepCard stepName="ClinicalExtract" step={step} isCurrentStep={false} defaultExpanded={true} />,
    )
    // Error appears in both header and expanded detail
    const errors = screen.getAllByText('Rate limit exceeded')
    expect(errors.length).toBeGreaterThanOrEqual(1)
  })

  it('shows result summary on header for completed step', () => {
    const step = makeStep('DocumentParse', 0, {
      resultSummaryJson: '{"pageCount":2,"fileSizeBytes":45056,"ocrConfidence":0.99}',
    })
    render(
      <ExtractionStepCard stepName="DocumentParse" step={step} isCurrentStep={false} defaultExpanded={false} />,
    )
    expect(screen.getByText('2 pages, 44 KB, OCR 99%')).toBeInTheDocument()
  })
})
