import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ViewModeSelector } from '../../../src/components/extraction/ViewModeSelector'

describe('ViewModeSelector', () => {
  it('renders all four mode buttons', () => {
    render(<ViewModeSelector value="raw" onChange={vi.fn()} />)

    expect(screen.getByRole('radio', { name: 'Raw' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Conversation' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Activity' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Summary' })).toBeInTheDocument()
  })

  it('marks selected mode as checked', () => {
    render(<ViewModeSelector value="conversation" onChange={vi.fn()} />)

    expect(screen.getByRole('radio', { name: 'Conversation' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('radio', { name: 'Raw' })).toHaveAttribute('aria-checked', 'false')
  })

  it('calls onChange when a mode button is clicked', async () => {
    const onChange = vi.fn()
    render(<ViewModeSelector value="raw" onChange={onChange} />)

    await userEvent.click(screen.getByRole('radio', { name: 'Activity' }))

    expect(onChange).toHaveBeenCalledWith('activity')
  })
})
