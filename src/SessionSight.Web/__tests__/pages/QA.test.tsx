import { describe, it, expect } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '../../src/test/mocks/server'
import { renderWithProviders } from '../../src/test/render'
import { QA } from '../../src/pages/QA'
import { mockQAResponse, mockQAResponseWithWarning } from '../../src/test/fixtures/qa'

function renderQA() {
  return renderWithProviders(<QA />)
}

describe('QA page', () => {
  it('displays page heading and patient selector', async () => {
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /q&a/i })).toBeInTheDocument(),
    )
    expect(screen.getByLabelText(/patient/i)).toBeInTheDocument()
  })

  it('populates patient dropdown with patients from API', async () => {
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )
    expect(screen.getByRole('option', { name: /Jane Smith/ })).toBeInTheDocument()
  })

  it('disables Ask button when no patient selected', async () => {
    renderQA()

    await waitFor(() =>
      expect(screen.getByLabelText(/patient/i)).toBeInTheDocument(),
    )
    expect(screen.getByRole('button', { name: /ask/i })).toBeDisabled()
  })

  it('disables Ask button when question is empty', async () => {
    const user = userEvent.setup()
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')

    expect(screen.getByRole('button', { name: /ask/i })).toBeDisabled()
  })

  it('shows loading state during submission', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/qa/patient/:patientId', () => {
        // Never resolves — keeps the mutation pending
        return new Promise(() => {})
      }),
    )

    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() => {
      expect(document.querySelector('.animate-spin')).toBeInTheDocument()
    })
    expect(screen.getByText(/Processing with AI/)).toBeInTheDocument()
  })

  it('clears question input after submission', async () => {
    const user = userEvent.setup()
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    expect(screen.getByLabelText(/question/i)).toHaveValue('')
  })

  it('displays answer and source after successful response', async () => {
    const user = userEvent.setup()
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/The patient discussed anxiety/)).toBeInTheDocument(),
    )
    expect(screen.getByText(/View session/)).toBeInTheDocument()
  })

  it('displays warning banner when response has warning', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/qa/patient/:patientId', () => {
        return HttpResponse.json(mockQAResponseWithWarning)
      }),
    )

    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/More than 10 sessions matched/)).toBeInTheDocument(),
    )
  })

  it('displays error banner on API failure', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('/api/qa/patient/:patientId', () => {
        return new HttpResponse('Patient not found', { status: 404 })
      }),
    )

    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/Patient not found/)).toBeInTheDocument(),
    )
  })

  it('accumulates multiple Q&A entries', async () => {
    const user = userEvent.setup()
    let callCount = 0
    server.use(
      http.post('/api/qa/patient/:patientId', () => {
        callCount++
        return HttpResponse.json({
          ...mockQAResponse,
          answer: `Answer ${callCount}: The patient discussed anxiety.`,
        })
      }),
    )

    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    // First question
    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'First question')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/Answer 1/)).toBeInTheDocument(),
    )

    // Second question
    await user.type(screen.getByLabelText(/question/i), 'Second question')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/Answer 2/)).toBeInTheDocument(),
    )

    // Both answers should be visible
    expect(screen.getByText(/Answer 1/)).toBeInTheDocument()
    expect(screen.getByText(/Answer 2/)).toBeInTheDocument()
  })

  it('clears chat history when patient changes', async () => {
    const user = userEvent.setup()
    renderQA()

    await waitFor(() =>
      expect(screen.getByRole('option', { name: /John Doe/ })).toBeInTheDocument(),
    )

    // Submit a question
    await user.selectOptions(screen.getByLabelText(/patient/i), 'p1')
    await user.type(screen.getByLabelText(/question/i), 'What concerns?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() =>
      expect(screen.getByText(/The patient discussed anxiety/)).toBeInTheDocument(),
    )

    // Change patient
    await user.selectOptions(screen.getByLabelText(/patient/i), 'p2')

    // Previous answer should be gone
    expect(screen.queryByText(/The patient discussed anxiety/)).not.toBeInTheDocument()
  })
})
