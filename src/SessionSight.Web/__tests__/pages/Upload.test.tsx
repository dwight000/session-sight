import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { Upload } from '../../src/pages/Upload'

function renderUpload() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Upload />
      </BrowserRouter>
    </QueryClientProvider>
  )
}

describe('Upload page', () => {
  it('displays loading state initially', () => {
    renderUpload()
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('displays session dropdown with sessions without documents', async () => {
    renderUpload()
    await waitFor(() => {
      expect(screen.getByLabelText(/select session/i)).toBeInTheDocument()
    })
  })

  it('shows warning when no sessions available', async () => {
    server.use(
      http.get('/api/sessions', () => HttpResponse.json([]))
    )

    renderUpload()
    await waitFor(() => {
      expect(screen.getByText(/no sessions available for upload/i)).toBeInTheDocument()
    })
  })

  it('shows file info when file selected', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    await user.upload(fileInput, file)

    await waitFor(() => {
      expect(screen.getByText(/selected file:/i)).toBeInTheDocument()
    })
  })

  it('shows submit button enabled when session and file selected', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Initially button is disabled
    const submitButton = screen.getByRole('button', { name: /upload & extract/i })
    expect(submitButton).toBeDisabled()

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Select file
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    await user.upload(fileInput, file)

    // Button should now be enabled
    await waitFor(() => {
      expect(submitButton).not.toBeDisabled()
    })
  })

  it('shows success message after successful upload', async () => {
    const user = userEvent.setup()
    // Default MSW handlers return success for upload/extraction
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Select file
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    await user.upload(fileInput, file)

    // Submit form
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    await waitFor(() => {
      expect(screen.getByText(/document uploaded and extraction completed successfully/i)).toBeInTheDocument()
    }, { timeout: 5000 })
  })

  it('shows error message when extraction fails', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/extraction/:sessionId', () => HttpResponse.json({ success: false, errorMessage: 'Invalid document format' }))
    )

    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Select file
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    await user.upload(fileInput, file)

    // Submit form
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    await waitFor(() => {
      expect(screen.getByText(/invalid document format/i)).toBeInTheDocument()
    }, { timeout: 5000 })
  })

  it('shows error message on network error', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/sessions/:sessionId/document', () => HttpResponse.error())
    )

    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Select file
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    await user.upload(fileInput, file)

    // Submit form
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    await waitFor(() => {
      // Should show some error (network error becomes generic error message)
      expect(screen.getByText(/failed to fetch/i)).toBeInTheDocument()
    }, { timeout: 5000 })
  })
})
