import { describe, it, expect } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { renderWithProviders } from '../../src/test/render'
import { ReviewQueue } from '../../src/pages/ReviewQueue'
import { mockReviewQueue } from '../../src/test/fixtures/review'

describe('ReviewQueue', () => {
  it('renders table rows from mock queue data', async () => {
    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    for (const item of mockReviewQueue) {
      expect(screen.getByText(item.patientName)).toBeInTheDocument()
    }
  })

  it('default filter is Pending', async () => {
    renderWithProviders(<ReviewQueue />)

    // Wait for the component to finish loading
    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    const select = document.querySelector('select') as HTMLSelectElement
    expect(select).not.toBeNull()
    expect(select.value).toBe('Pending')
  })

  it('changing filter dropdown triggers refetch', async () => {
    const user = userEvent.setup()
    let requestedStatus: string | null = null

    server.use(
      http.get('/api/review/queue', ({ request }) => {
        const url = new URL(request.url)
        requestedStatus = url.searchParams.get('status')
        return HttpResponse.json(mockReviewQueue)
      }),
    )

    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    const select = screen.getByRole('combobox')
    await user.selectOptions(select, 'Approved')

    await waitFor(() => {
      expect(requestedStatus).toBe('Approved')
    })
  })

  it('client-side sorting: click Session Date header toggles sort direction', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    // Default sort is sessionDate desc — Carol (2025-01-16) first
    const rows = screen.getAllByRole('row')
    // Row 0 is thead, data rows start at 1
    const firstDataRow = rows[1]
    expect(within(firstDataRow).getByText('Carol Davis')).toBeInTheDocument()

    // Click Session Date to toggle to asc
    const sessionDateHeader = screen.getByText(/Session Date/)
    await user.click(sessionDateHeader)

    // Now Bob (2025-01-14) should be first
    const rowsAfter = screen.getAllByRole('row')
    const firstAfter = rowsAfter[1]
    expect(within(firstAfter).getByText('Bob Smith')).toBeInTheDocument()
  })

  it('client-side sorting: click Confidence header sorts by numeric value', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    // Click Confidence header - first click sets desc
    const confHeader = screen.getByText(/Confidence/)
    await user.click(confHeader)

    const rows = screen.getAllByRole('row')
    const firstDataRow = rows[1]
    // Highest confidence is Carol (0.95 = 95%)
    expect(within(firstDataRow).getByText('95%')).toBeInTheDocument()
  })

  it('shows empty state when no results', async () => {
    server.use(
      http.get('/api/review/queue', () => {
        return HttpResponse.json([])
      }),
    )

    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('No sessions match the current filters.')).toBeInTheDocument()
    })
  })

  it('shows spinner during loading', () => {
    server.use(
      http.get('/api/review/queue', () => {
        return new Promise(() => {}) // never resolves
      }),
    )

    renderWithProviders(<ReviewQueue />)

    const spinner = document.querySelector('.animate-spin')
    expect(spinner).toBeInTheDocument()
  })

  it('review link navigates to /review/session/{sessionId}', async () => {
    renderWithProviders(<ReviewQueue />)

    await waitFor(() => {
      expect(screen.getByText('Review Queue')).toBeInTheDocument()
    })

    const reviewLinks = screen.getAllByRole('link')
    const sessionLink = reviewLinks.find(
      (link) => link.getAttribute('href') === `/review/session/${mockReviewQueue[0].sessionId}`,
    )
    expect(sessionLink).toBeInTheDocument()
  })
})
