import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ExtractionStepCard } from '../../../src/components/extraction/ExtractionStepCard'
import { makeStep } from '../../../src/test/fixtures/extractionSteps'
import type { ReactElement } from 'react'

function renderWithQuery(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
  )
}

describe('ExtractionStepCard', () => {
  it('renders pending state with grayed-out name', () => {
    renderWithQuery(
      <ExtractionStepCard stepName="DocumentParse" step={undefined} isCurrentStep={false} defaultExpanded={false} sessionId="sess-001" />,
    )
    expect(screen.getByText('Document Parse')).toBeInTheDocument()
    // Pending cards are clickable (show placeholder when expanded)
    const btn = screen.getByRole('button')
    expect(btn).not.toBeDisabled()
  })

  it('shows placeholder grid when pending and expanded', () => {
    renderWithQuery(
      <ExtractionStepCard stepName="DocumentParse" step={undefined} isCurrentStep={false} defaultExpanded={true} sessionId="sess-001" />,
    )
    expect(screen.getByText('Duration:')).toBeInTheDocument()
    expect(screen.getByText('Tokens:')).toBeInTheDocument()
    expect(screen.getAllByText('--').length).toBeGreaterThanOrEqual(3)
  })

  it('renders running state with spinner text', () => {
    renderWithQuery(
      <ExtractionStepCard stepName="Intake" step={undefined} isCurrentStep={true} defaultExpanded={false} sessionId="sess-001" />,
    )
    expect(screen.getByText('Running...')).toBeInTheDocument()
  })

  it('renders completed step with duration', () => {
    const step = makeStep('DocumentParse', 0, { durationMs: 1200 })
    renderWithQuery(
      <ExtractionStepCard stepName="DocumentParse" step={step} isCurrentStep={false} defaultExpanded={false} sessionId="sess-001" />,
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
    renderWithQuery(
      <ExtractionStepCard stepName="ClinicalExtract" step={step} isCurrentStep={false} defaultExpanded={false} sessionId="sess-001" />,
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
    renderWithQuery(
      <ExtractionStepCard stepName="ClinicalExtract" step={step} isCurrentStep={false} defaultExpanded={true} sessionId="sess-001" />,
    )
    // Error appears in both header and expanded detail
    const errors = screen.getAllByText('Rate limit exceeded')
    expect(errors.length).toBeGreaterThanOrEqual(1)
  })

  it('shows result summary on header for completed step', () => {
    const step = makeStep('DocumentParse', 0, {
      resultSummaryJson: '{"pageCount":2,"fileSizeBytes":45056,"ocrConfidence":0.99}',
    })
    renderWithQuery(
      <ExtractionStepCard stepName="DocumentParse" step={step} isCurrentStep={false} defaultExpanded={false} sessionId="sess-001" />,
    )
    expect(screen.getByText('2 pages, 44 KB, OCR 99%')).toBeInTheDocument()
  })

  it('shows confidence heatmap for ClinicalExtract step when expanded', async () => {
    const step = makeStep('ClinicalExtract', 2, { durationMs: 15000 })
    renderWithQuery(
      <ExtractionStepCard stepName="ClinicalExtract" step={step} isCurrentStep={false} defaultExpanded={true} sessionId="sess-001" />,
    )

    await waitFor(() => {
      expect(screen.getByText(/Field Confidence/)).toBeInTheDocument()
    })
  })

  it('shows risk merge view for RiskAssess step when expanded', async () => {
    const step = makeStep('RiskAssess', 3, { durationMs: 4500 })
    renderWithQuery(
      <ExtractionStepCard stepName="RiskAssess" step={step} isCurrentStep={false} defaultExpanded={true} sessionId="sess-001" />,
    )

    await waitFor(() => {
      expect(screen.getByText(/Risk Merge/)).toBeInTheDocument()
    })
  })
})
