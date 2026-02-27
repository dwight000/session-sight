import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { LegalDisclaimer } from '../../../src/components/layout/LegalDisclaimer'
import { Sidebar } from '../../../src/components/layout/Sidebar'
import { MobileNav } from '../../../src/components/layout/MobileNav'
import { userEvent } from '@testing-library/user-event'

describe('LegalDisclaimer', () => {
  it('renders heading text', () => {
    render(<LegalDisclaimer />)
    expect(screen.getByText(/Demo Only/)).toBeInTheDocument()
    expect(screen.getByText(/Not for Clinical Use/)).toBeInTheDocument()
  })

  it('renders liability language', () => {
    render(<LegalDisclaimer />)
    expect(
      screen.getByText(/No liability is accepted/),
    ).toBeInTheDocument()
  })

  it('renders portfolio disclaimer text', () => {
    render(<LegalDisclaimer />)
    expect(
      screen.getByText(/portfolio application/),
    ).toBeInTheDocument()
  })
})

describe('Sidebar with LegalDisclaimer', () => {
  it('renders disclaimer within sidebar', () => {
    render(
      <MemoryRouter>
        <Sidebar />
      </MemoryRouter>,
    )
    expect(screen.getByText(/Not for Clinical Use/)).toBeInTheDocument()
  })
})

describe('MobileNav with disclaimer', () => {
  it('shows disclaimer when menu is open', async () => {
    render(
      <MemoryRouter>
        <MobileNav />
      </MemoryRouter>,
    )
    const button = screen.getByRole('button')
    await userEvent.click(button)
    expect(
      screen.getByText(/AI outputs are not for clinical use/),
    ).toBeInTheDocument()
  })

  it('hides disclaimer when menu is closed', () => {
    render(
      <MemoryRouter>
        <MobileNav />
      </MemoryRouter>,
    )
    expect(
      screen.queryByText(/AI outputs are not for clinical use/),
    ).not.toBeInTheDocument()
  })
})
