import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Badge } from '../../../src/components/ui/Badge'

describe('Badge', () => {
  it('renders children text', () => {
    render(<Badge>Hello</Badge>)
    expect(screen.getByText('Hello')).toBeInTheDocument()
  })

  it('uses default variant when none specified', () => {
    const { container } = render(<Badge>Default</Badge>)
    const span = container.querySelector('span')!
    expect(span.className).toContain('bg-gray-100')
    expect(span.className).toContain('text-gray-800')
  })

  it.each([
    ['default', 'bg-gray-100'],
    ['pending', 'bg-yellow-100'],
    ['approved', 'bg-green-100'],
    ['dismissed', 'bg-gray-200'],
    ['danger', 'bg-red-100'],
    ['warning', 'bg-amber-100'],
    ['success', 'bg-green-100'],
    ['purple', 'bg-purple-100'],
  ] as const)('variant "%s" applies correct class', (variant, expectedClass) => {
    const { container } = render(<Badge variant={variant}>Test</Badge>)
    const span = container.querySelector('span')!
    expect(span.className).toContain(expectedClass)
  })

  it('falls back to default for unknown variant', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const { container } = render(<Badge variant={'nonexistent' as any}>Fallback</Badge>)
    const span = container.querySelector('span')!
    expect(span.className).toContain('bg-gray-100')
  })

  it('appends custom className', () => {
    const { container } = render(<Badge className="my-custom">Styled</Badge>)
    const span = container.querySelector('span')!
    expect(span.className).toContain('my-custom')
  })
})
