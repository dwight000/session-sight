import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfidenceHeatmap } from '../../../src/components/extraction/ConfidenceHeatmap'
import { mockExtractionResult } from '../../../src/test/fixtures/extractionSteps'

describe('ConfidenceHeatmap', () => {
  it('returns null when data is undefined', () => {
    const { container } = render(<ConfidenceHeatmap data={undefined} />)
    expect(container.innerHTML).toBe('')
  })

  it('shows field count in toggle button', () => {
    render(<ConfidenceHeatmap data={mockExtractionResult.data} />)
    expect(screen.getByText(/Field Confidence \(\d+ fields\)/)).toBeInTheDocument()
  })

  it('renders chips with correct color classes when open', () => {
    render(<ConfidenceHeatmap data={mockExtractionResult.data} defaultOpen={true} />)

    // phq9Score has confidence 0.45 (< 0.5) — red
    const phq9Chip = screen.getByTitle(/Phq9 Score: 45%/)
    expect(phq9Chip.className).toContain('bg-red-100')

    // overallRiskLevel has confidence 0.72 (0.5-0.8) — yellow
    const riskChip = screen.getByTitle(/Overall Risk Level: 72%/)
    expect(riskChip.className).toContain('bg-yellow-100')

    // sessionDate has confidence 0.98 (> 0.8) — green
    const dateChip = screen.getByTitle(/Session Date: 98%/)
    expect(dateChip.className).toContain('bg-green-100')
  })

  it('expands source detail on chip click', async () => {
    const user = userEvent.setup()
    render(<ConfidenceHeatmap data={mockExtractionResult.data} defaultOpen={true} />)

    // Click the session date chip
    const dateChip = screen.getByTitle(/Session Date: 98%/)
    await user.click(dateChip)

    // Source detail should appear
    expect(screen.getByText(/Section:/)).toBeInTheDocument()
    expect(screen.getByText('header')).toBeInTheDocument()
    expect(screen.getByText(/Session date: January 15, 2025/)).toBeInTheDocument()
  })

  it('collapses source detail on second chip click', async () => {
    const user = userEvent.setup()
    render(<ConfidenceHeatmap data={mockExtractionResult.data} defaultOpen={true} />)

    const dateChip = screen.getByTitle(/Session Date: 98%/)
    await user.click(dateChip)
    expect(screen.getByText(/Chars:/)).toBeInTheDocument()

    await user.click(dateChip)
    expect(screen.queryByText(/Chars:/)).not.toBeInTheDocument()
  })

  it('starts closed by default', () => {
    render(<ConfidenceHeatmap data={mockExtractionResult.data} />)
    // Chips should not be visible
    expect(screen.queryByTitle(/Session Date/)).not.toBeInTheDocument()
  })

  it('excludes metadata section', () => {
    const dataWithMetadata = {
      ...mockExtractionResult.data,
      metadata: {
        internalField: { value: 'test', confidence: 0.5, source: null },
      },
    }
    render(<ConfidenceHeatmap data={dataWithMetadata} defaultOpen={true} />)
    expect(screen.queryByTitle(/Internal Field/)).not.toBeInTheDocument()
  })
})
