import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { Therapists } from '../../src/pages/Therapists'

function renderTherapists() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Therapists />
      </BrowserRouter>
    </QueryClientProvider>
  )
}

describe('Therapists page', () => {
  it('displays loading state initially', () => {
    renderTherapists()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('displays therapist list', async () => {
    renderTherapists()
    await waitFor(() => {
      expect(screen.getByText('Default Therapist')).toBeInTheDocument()
      expect(screen.getByText('Dr. Jane Wilson')).toBeInTheDocument()
    })
  })

  it('shows add therapist form when clicking Add Therapist', async () => {
    renderTherapists()
    await waitFor(() => expect(screen.getByText('Default Therapist')).toBeInTheDocument())

    const addButton = screen.getByRole('button', { name: /add therapist/i })
    await userEvent.click(addButton)

    expect(screen.getByLabelText(/name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/license number/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/credentials/i)).toBeInTheDocument()
  })

  it('creates therapist on form submit', async () => {
    renderTherapists()
    await waitFor(() => expect(screen.getByText('Default Therapist')).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: /add therapist/i }))

    await userEvent.type(screen.getByLabelText(/^name$/i), 'Dr. Test')
    await userEvent.type(screen.getByLabelText(/license number/i), 'LIC-9999')
    await userEvent.type(screen.getByLabelText(/credentials/i), 'LCSW')

    await userEvent.click(screen.getByRole('button', { name: /create therapist/i }))

    await waitFor(() => {
      expect(screen.queryByLabelText(/^name$/i)).not.toBeInTheDocument()
    })
  })

  it('displays empty state when no therapists', async () => {
    server.use(http.get('/api/therapists', () => HttpResponse.json([])))
    renderTherapists()

    await waitFor(() => {
      expect(screen.getByText(/no therapists yet/i)).toBeInTheDocument()
    })
  })
})
