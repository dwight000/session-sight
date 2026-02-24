import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { Upload } from '../../src/pages/Upload'

const sampleData = [
  {
    id: 'sample-nonrisk-001',
    filename: 'sample-nonrisk-001.pdf',
    title: 'Anxiety / CBT Session',
    description: 'GAD with cognitive restructuring',
    previewText: 'Session Note - March 5, 2026 Patient: Sarah Chen...',
  },
]

beforeEach(() => {
  // Mock samples.json fetch
  server.use(
    http.get('/samples/samples.json', () => HttpResponse.json(sampleData))
  )
})

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

  it('shows document source tabs', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    expect(screen.getByRole('tab', { name: /sample documents/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /your document/i })).toBeInTheDocument()
  })

  it('shows sample document cards by default', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    await waitFor(() => {
      expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument()
    })
  })

  it('toggles preview text on sample card', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    await user.click(screen.getByText('Preview'))
    expect(screen.getByText(/Session Note - March 5, 2026/)).toBeInTheDocument()

    await user.click(screen.getByText('Hide'))
    expect(screen.queryByText(/Session Note - March 5, 2026/)).not.toBeInTheDocument()
  })

  it('selects sample document via Use This button', async () => {
    const user = userEvent.setup()
    const pdfBlob = new Blob(['%PDF-1.4'], { type: 'application/pdf' })
    server.use(
      http.get('/samples/sample-nonrisk-001.pdf', () => new HttpResponse(pdfBlob))
    )

    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    await user.click(screen.getByText('Use This'))

    await waitFor(() => {
      expect(screen.getByText(/selected file:/i)).toBeInTheDocument()
    })
  })

  it('shows file input when Your Document tab selected', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    await user.click(screen.getByRole('tab', { name: /your document/i }))
    expect(screen.getByLabelText(/document file/i)).toBeInTheDocument()
  })

  it('shows file info when file selected via file input', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    await user.click(screen.getByRole('tab', { name: /your document/i }))
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

    // Switch to file input
    await user.click(screen.getByRole('tab', { name: /your document/i }))

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
    // Default MSW handlers return 202 for extraction and Completed steps for extraction steps
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Switch to file input and select file
    await user.click(screen.getByRole('tab', { name: /your document/i }))
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    await user.upload(fileInput, file)

    // Submit form
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    // Banner is driven by polling extraction steps (documentStatus === 'Completed')
    await waitFor(() => {
      expect(screen.getByText(/extraction completed successfully/i)).toBeInTheDocument()
    }, { timeout: 5000 })
  })

  it('shows error message when extraction request fails', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/extraction/:sessionId', () =>
        new HttpResponse('Invalid document format', { status: 400 })
      )
    )

    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Select session
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')

    // Switch to file input and select file
    await user.click(screen.getByRole('tab', { name: /your document/i }))
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    await user.upload(fileInput, file)

    // Submit form
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    await waitFor(() => {
      expect(screen.getByText(/extraction failed/i)).toBeInTheDocument()
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

    // Switch to file input and select file
    await user.click(screen.getByRole('tab', { name: /your document/i }))
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
