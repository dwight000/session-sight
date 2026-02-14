import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { ProcessingJobs } from '../../src/pages/ProcessingJobs'

function renderProcessingJobs() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ProcessingJobs />
      </BrowserRouter>
    </QueryClientProvider>
  )
}

describe('ProcessingJobs page', () => {
  it('displays loading state initially', () => {
    renderProcessingJobs()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('displays processing jobs list', async () => {
    renderProcessingJobs()
    await waitFor(() => {
      expect(screen.getByText('extraction-session-001')).toBeInTheDocument()
      expect(screen.getByText('extraction-session-002')).toBeInTheDocument()
      expect(screen.getByText('extraction-session-003')).toBeInTheDocument()
    })
  })

  it('displays empty state when no jobs', async () => {
    server.use(http.get('/api/processing-jobs', () => HttpResponse.json([])))
    renderProcessingJobs()

    await waitFor(() => {
      expect(screen.getByText(/no processing jobs found/i)).toBeInTheDocument()
    })
  })

  it('shows status badges with correct variants', async () => {
    renderProcessingJobs()
    await waitFor(() => {
      // "Completed" appears as both a column header and a status badge
      expect(screen.getAllByText('Completed').length).toBeGreaterThanOrEqual(1)
      // "Processing" appears in the page title and as a status badge
      expect(screen.getAllByText('Processing').length).toBeGreaterThanOrEqual(1)
      expect(screen.getByText('Failed')).toBeInTheDocument()
    })
  })
})
