import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button } from '../../../src/components/ui/Button'

describe('Button', () => {
  it('renders children', () => {
    render(<Button>Click me</Button>)
    expect(screen.getByText('Click me')).toBeInTheDocument()
  })

  it.each([
    ['primary', 'bg-blue-600'],
    ['secondary', 'bg-white'],
    ['danger', 'bg-red-600'],
    ['success', 'bg-green-600'],
  ] as const)('variant "%s" applies %s class', (variant, expectedClass) => {
    const { container } = render(<Button variant={variant}>Test</Button>)
    const button = container.querySelector('button')!
    expect(button.className).toContain(expectedClass)
  })

  it('falls back to primary for unknown variant', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const { container } = render(<Button variant={'nonexistent' as any}>Fallback</Button>)
    const button = container.querySelector('button')!
    expect(button.className).toContain('bg-blue-600')
  })

  it('passes through onClick, disabled, and type', async () => {
    const user = userEvent.setup()
    let clicked = false
    render(
      <Button type="submit" onClick={() => { clicked = true }}>
        Submit
      </Button>,
    )

    const button = screen.getByRole('button', { name: 'Submit' })
    expect(button).toHaveAttribute('type', 'submit')

    await user.click(button)
    expect(clicked).toBe(true)
  })

  it('sets disabled attribute and opacity class', () => {
    const { container } = render(<Button disabled>Disabled</Button>)
    const button = container.querySelector('button')!
    expect(button).toBeDisabled()
    expect(button.className).toContain('disabled:opacity-50')
  })
})
