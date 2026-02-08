import { describe, it, expect } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
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
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    await waitFor(() => {
      expect(screen.getByText(/selected file:/i)).toBeInTheDocument()
    })
  })

  it('shows submit button enabled when session and file selected', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByLabelText(/select session/i)).toBeInTheDocument())

    // Initially button is disabled
    const submitButton = screen.getByRole('button', { name: /upload & extract/i })
    expect(submitButton).toBeDisabled()

    // Select session
    const sessionSelect = screen.getByLabelText(/select session/i)
    fireEvent.change(sessionSelect, { target: { value: 's1' } })

    // Select file
    const fileInput = screen.getByLabelText(/document file/i) as HTMLInputElement
    const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' })
    fireEvent.change(fileInput, { target: { files: [file] } })

    // Button should now be enabled
    await waitFor(() => {
      expect(submitButton).not.toBeDisabled()
    })
  })
})
