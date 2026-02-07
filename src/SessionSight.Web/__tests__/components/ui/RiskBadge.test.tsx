import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { RiskBadge } from '../../../src/components/ui/RiskBadge'

describe('RiskBadge', () => {
  it.each([
    ['Low', 'bg-green-100'],
    ['Moderate', 'bg-amber-100'],
    ['High', 'bg-red-100'],
    ['Imminent', 'bg-purple-100'],
  ])('renders "%s" risk level with correct variant', (level, expectedClass) => {
    const { container } = render(<RiskBadge level={level} />)
    const span = container.querySelector('span')!
    expect(span.className).toContain(expectedClass)
    expect(screen.getByText(level)).toBeInTheDocument()
  })

  it('handles case insensitivity', () => {
    const { container } = render(<RiskBadge level="HIGH" />)
    const span = container.querySelector('span')!
    expect(span.className).toContain('bg-red-100')
  })

  it('falls back to default for unknown level', () => {
    const { container } = render(<RiskBadge level="Unknown" />)
    const span = container.querySelector('span')!
    expect(span.className).toContain('bg-gray-100')
  })

  it('displays original text as provided', () => {
    render(<RiskBadge level="HIGH" />)
    expect(screen.getByText('HIGH')).toBeInTheDocument()
  })
})
