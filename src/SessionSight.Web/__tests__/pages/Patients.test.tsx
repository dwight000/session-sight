import { describe, it, expect } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { Patients } from '../../src/pages/Patients'

function renderPatients() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Patients />
      </BrowserRouter>
    </QueryClientProvider>
  )
}

describe('Patients page', () => {
  it('displays loading state initially', () => {
    renderPatients()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('displays patient list', async () => {
    renderPatients()
    await waitFor(() => {
      expect(screen.getByText('John Doe')).toBeInTheDocument()
      expect(screen.getByText('Jane Smith')).toBeInTheDocument()
    })
  })

  it('shows add patient form when clicking Add Patient', async () => {
    renderPatients()
    await waitFor(() => expect(screen.getByText('John Doe')).toBeInTheDocument())

    const addButton = screen.getByRole('button', { name: /add patient/i })
    await userEvent.click(addButton)

    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/date of birth/i)).toBeInTheDocument()
  })

  it('creates patient on form submit', async () => {
    renderPatients()
    await waitFor(() => expect(screen.getByText('John Doe')).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: /add patient/i }))

    await userEvent.type(screen.getByLabelText(/first name/i), 'Test')
    await userEvent.type(screen.getByLabelText(/last name/i), 'User')
    await userEvent.type(screen.getByLabelText(/external id/i), 'EXT003')
    fireEvent.change(screen.getByLabelText(/date of birth/i), { target: { value: '2000-01-01' } })

    await userEvent.click(screen.getByRole('button', { name: /create patient/i }))

    await waitFor(() => {
      expect(screen.queryByLabelText(/first name/i)).not.toBeInTheDocument()
    })
  })

  it('displays empty state when no patients', async () => {
    server.use(http.get('/api/patients', () => HttpResponse.json([])))
    renderPatients()

    await waitFor(() => {
      expect(screen.getByText(/no patients yet/i)).toBeInTheDocument()
    })
  })
})
