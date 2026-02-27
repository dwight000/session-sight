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
    riskTier: 'low',
    diagnosis: 'F41.1 — Generalized Anxiety Disorder',
    metaStrip: 'GAD · Low Risk · PsyD · Individual · In-Person',
    keyExtractions: [
      { field: 'Mood', value: '6/10 (worried but managing)' },
      { field: 'Interventions', value: 'Cognitive restructuring, relaxation training' },
    ],
    clinicalNote: 'Typical mid-treatment CBT case with good field coverage.',
  },
  {
    id: 'sample-risk-001',
    filename: 'sample-risk-001.pdf',
    title: 'Active SI with Safety Plan',
    description: 'High risk - specific plan',
    previewText: 'Session Note - March 20, 2026 Patient: Rachel Morrison...',
    riskTier: 'high',
    diagnosis: 'F33.2 — MDD, recurrent, severe',
    metaStrip: 'MDD Severe · High Risk · LCSW · Crisis · In-Person',
    keyExtractions: [
      { field: 'Risk Level', value: 'High — active ideation with specific plan' },
      { field: 'Safety Plan', value: 'Updated this session' },
    ],
    clinicalNote: 'Demonstrates capture of granular lethality markers.',
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

  it('shows risk badges on sample cards', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    expect(screen.getByText('High Risk')).toBeInTheDocument()
    expect(screen.getByText('Low Risk')).toBeInTheDocument()
  })

  it('sorts cards risk-first (high before low)', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    const highRisk = screen.getByText('High Risk')
    const lowRisk = screen.getByText('Low Risk')
    // High risk card should appear before low risk in the DOM
    expect(highRisk.compareDocumentPosition(lowRisk) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('shows key extractions on sample cards', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    expect(screen.getByText('Mood')).toBeInTheDocument()
    expect(screen.getByText('6/10 (worried but managing)')).toBeInTheDocument()
    expect(screen.getByText('Risk Level')).toBeInTheDocument()
    expect(screen.getByText('High — active ideation with specific plan')).toBeInTheDocument()
  })

  it('shows meta strip on sample cards', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    expect(screen.getByText('GAD · Low Risk · PsyD · Individual · In-Person')).toBeInTheDocument()
    expect(screen.getByText('MDD Severe · High Risk · LCSW · Crisis · In-Person')).toBeInTheDocument()
  })

  it('toggles "Why this sample" clinical note', async () => {
    const user = userEvent.setup()
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    // Clinical note not visible initially
    expect(screen.queryByText(/Typical mid-treatment CBT case/)).not.toBeInTheDocument()

    // Click to expand — find the first one (for the high risk card which renders first)
    const toggleButtons = screen.getAllByText(/Why this sample/)
    await user.click(toggleButtons[0])
    expect(screen.getByText(/Demonstrates capture of granular lethality markers/)).toBeInTheDocument()

    // Click to collapse
    await user.click(toggleButtons[0])
    expect(screen.queryByText(/Demonstrates capture of granular lethality markers/)).not.toBeInTheDocument()
  })

  it('renders Open PDF links with correct hrefs', async () => {
    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    const pdfLinks = screen.getAllByText('Open PDF ↗')
    expect(pdfLinks).toHaveLength(2)
    expect(pdfLinks[0].closest('a')).toHaveAttribute('href', '/samples/sample-risk-001.pdf')
    expect(pdfLinks[1].closest('a')).toHaveAttribute('href', '/samples/sample-nonrisk-001.pdf')
  })

  it('selects sample document via Use This button', async () => {
    const user = userEvent.setup()
    const pdfBlob = new Blob(['%PDF-1.4'], { type: 'application/pdf' })
    server.use(
      http.get('/samples/sample-nonrisk-001.pdf', () => new HttpResponse(pdfBlob))
    )

    renderUpload()
    await waitFor(() => expect(screen.getByText('Anxiety / CBT Session')).toBeInTheDocument())

    const useButtons = screen.getAllByText('Use This')
    await user.click(useButtons[1]) // second card is the low-risk one

    await waitFor(() => {
      expect(screen.getByText(/selected file:/i)).toBeInTheDocument()
    })
  })

  it('shows expected outcome card after submitting a sample', async () => {
    const user = userEvent.setup()
    const pdfBlob = new Blob(['%PDF-1.4'], { type: 'application/pdf' })
    server.use(
      http.get('/samples/sample-risk-001.pdf', () => new HttpResponse(pdfBlob))
    )

    renderUpload()
    await waitFor(() => expect(screen.getByText('Active SI with Safety Plan')).toBeInTheDocument())

    // Select the high-risk sample (first card)
    const useButtons = screen.getAllByText('Use This')
    await user.click(useButtons[0])
    await waitFor(() => expect(screen.getByText(/selected file:/i)).toBeInTheDocument())

    // Select session and submit
    await user.selectOptions(screen.getByLabelText(/select session/i), 's1')
    await user.click(screen.getByRole('button', { name: /upload & extract/i }))

    // Expected outcome card should appear with the sample's metadata
    await waitFor(() => {
      expect(screen.getByText('Expected Outcome')).toBeInTheDocument()
    })
    // Key extractions appear in both the expected outcome card and the sample card list,
    // so verify at least 2 instances exist (one from each)
    expect(screen.getAllByText('High — active ideation with specific plan')).toHaveLength(2)
    expect(screen.getAllByText('Updated this session')).toHaveLength(2)
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
