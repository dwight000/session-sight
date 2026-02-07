import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ConfidenceBar } from '../../../src/components/ui/ConfidenceBar'

describe('ConfidenceBar', () => {
  it('shows red color for confidence < 0.7', () => {
    const { container } = render(<ConfidenceBar value={0.5} />)
    const bar = container.querySelector('.bg-red-500')
    expect(bar).toBeInTheDocument()
  })

  it('shows yellow color for confidence >= 0.7 and < 0.9', () => {
    const { container } = render(<ConfidenceBar value={0.75} />)
    const bar = container.querySelector('.bg-yellow-500')
    expect(bar).toBeInTheDocument()
  })

  it('shows green color for confidence >= 0.9', () => {
    const { container } = render(<ConfidenceBar value={0.95} />)
    const bar = container.querySelector('.bg-green-500')
    expect(bar).toBeInTheDocument()
  })

  it('rounds percentage display correctly', () => {
    render(<ConfidenceBar value={0.876} />)
    expect(screen.getByText('88%')).toBeInTheDocument()
  })

  it('sets bar width style to percentage', () => {
    const { container } = render(<ConfidenceBar value={0.65} />)
    const bar = container.querySelector('.bg-red-500') as HTMLElement
    expect(bar.style.width).toBe('65%')
  })
})
