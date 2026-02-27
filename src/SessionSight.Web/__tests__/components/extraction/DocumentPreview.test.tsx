import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DocumentPreview } from '../../../src/components/extraction/DocumentPreview'

describe('DocumentPreview', () => {
  it('renders toggle button', () => {
    render(<DocumentPreview sessionId="sess-001" />)

    expect(screen.getByText('Document Preview')).toBeInTheDocument()
  })

  it('does not render iframe by default', () => {
    render(<DocumentPreview sessionId="sess-001" />)

    expect(screen.queryByTitle('Document preview')).not.toBeInTheDocument()
  })

  it('renders iframe with correct src on click', async () => {
    render(<DocumentPreview sessionId="sess-001" />)

    await userEvent.click(screen.getByText('Document Preview'))

    const iframe = screen.getByTitle('Document preview') as HTMLIFrameElement
    expect(iframe).toBeInTheDocument()
    expect(iframe.src).toContain('/api/sessions/sess-001/document/download')
  })

  it('renders iframe immediately when defaultOpen is true', () => {
    render(<DocumentPreview sessionId="sess-001" defaultOpen />)

    expect(screen.getByTitle('Document preview')).toBeInTheDocument()
  })

  it('collapses iframe on second click', async () => {
    render(<DocumentPreview sessionId="sess-001" />)

    await userEvent.click(screen.getByText('Document Preview'))
    expect(screen.getByTitle('Document preview')).toBeInTheDocument()

    await userEvent.click(screen.getByText('Document Preview'))
    expect(screen.queryByTitle('Document preview')).not.toBeInTheDocument()
  })

  it('has aria-expanded attribute', async () => {
    render(<DocumentPreview sessionId="sess-001" />)

    const button = screen.getByText('Document Preview').closest('button')!
    expect(button).toHaveAttribute('aria-expanded', 'false')

    await userEvent.click(button)
    expect(button).toHaveAttribute('aria-expanded', 'true')
  })
})
