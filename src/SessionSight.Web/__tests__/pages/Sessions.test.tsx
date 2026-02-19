import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { Sessions } from '../../src/pages/Sessions'

function renderSessions() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Sessions />
      </BrowserRouter>
    </QueryClientProvider>
  )
}

describe('Sessions page', () => {
  it('displays loading state initially', () => {
    renderSessions()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('displays session list', async () => {
    renderSessions()
    await waitFor(() => {
      // Both mock sessions are Individual type
      expect(screen.getAllByText('Individual').length).toBeGreaterThanOrEqual(1)
    })
  })

  it('shows document status badges', async () => {
    renderSessions()
    await waitFor(() => {
      expect(screen.getByText('Extracted')).toBeInTheDocument()
      expect(screen.getByText('No Document')).toBeInTheDocument()
      expect(screen.getByText('Failed')).toBeInTheDocument()
    })
  })

  it('shows retry button for failed sessions', async () => {
    renderSessions()
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
    })
  })

  it('retry button calls extraction API', async () => {
    let capturedSessionId = ''
    server.use(
      http.post('/api/extraction/:sessionId', ({ params }) => {
        capturedSessionId = params.sessionId as string
        return HttpResponse.json({ success: true, extractionId: 'new-id' })
      }),
    )

    renderSessions()
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /retry/i }))

    await waitFor(() => expect(capturedSessionId).toBe('s3'))
  })

  it('shows add session form when clicking Add Session', async () => {
    renderSessions()
    await waitFor(() => expect(screen.getAllByText('Individual').length).toBeGreaterThanOrEqual(1))

    await userEvent.click(screen.getByRole('button', { name: /add session/i }))

    expect(screen.getByLabelText(/patient/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/session date/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/session type/i)).toBeInTheDocument()
  })

  it('shows therapist dropdown in form', async () => {
    renderSessions()
    await waitFor(() => expect(screen.getAllByText('Individual').length).toBeGreaterThanOrEqual(1))

    await userEvent.click(screen.getByRole('button', { name: /add session/i }))

    expect(screen.getByLabelText(/therapist/i)).toBeInTheDocument()
  })

  it('displays empty state when no sessions', async () => {
    server.use(
      http.get('/api/sessions', () => HttpResponse.json([]))
    )

    renderSessions()
    await waitFor(() => {
      expect(screen.getByText(/no sessions found/i)).toBeInTheDocument()
    })
  })
})
