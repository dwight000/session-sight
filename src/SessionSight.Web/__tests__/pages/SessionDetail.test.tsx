import { describe, it, expect } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { Routes, Route } from 'react-router-dom'
import { server } from '../../src/test/mocks/server'
import { renderWithProviders } from '../../src/test/render'
import { SessionDetail } from '../../src/pages/SessionDetail'
import { mockReviewDetail, mockApprovedDetail } from '../../src/test/fixtures/review'

function renderSessionDetail(sessionId = 'sess-001') {
  return renderWithProviders(
    <Routes>
      <Route path="/review/session/:sessionId" element={<SessionDetail />} />
    </Routes>,
    { route: `/review/session/${sessionId}` },
  )
}

describe('SessionDetail', () => {
  it('renders patient name, date, status badge, confidence', async () => {
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText(mockReviewDetail.patientName)).toBeInTheDocument()
    })

    // Status badge text may appear in multiple places (badge + review history)
    expect(screen.getAllByText(mockReviewDetail.reviewStatus).length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(`${Math.round(mockReviewDetail.overallConfidence * 100)}% confidence`)).toBeInTheDocument()
  })

  it('parses and renders session summary from summaryJson', async () => {
    renderSessionDetail()

    const summary = JSON.parse(mockReviewDetail.summaryJson!)

    await waitFor(() => {
      expect(screen.getByText(summary.oneLiner)).toBeInTheDocument()
    })

    expect(screen.getByText(summary.keyPoints)).toBeInTheDocument()
    expect(screen.getByText(summary.nextSessionFocus)).toBeInTheDocument()
  })

  it('renders review reasons as bullet list', async () => {
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Review Reasons')).toBeInTheDocument()
    })

    for (const reason of mockReviewDetail.reviewReasons) {
      expect(screen.getByText(reason)).toBeInTheDocument()
    }

    // Verify they're in a list
    const listItems = screen.getAllByRole('listitem')
    expect(listItems.length).toBe(mockReviewDetail.reviewReasons.length)
  })

  it('risk assessment accordion starts open', async () => {
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Risk Assessment')).toBeInTheDocument()
    })

    // The risk assessment section content should be visible
    expect(screen.getByText('Suicidal Ideation')).toBeInTheDocument()
    expect(screen.getByText('Overall Risk Level')).toBeInTheDocument()
  })

  it('other accordion sections start closed, open on click', async () => {
    const user = userEvent.setup()
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Session Info')).toBeInTheDocument()
    })

    // Session Info content should not be visible initially
    expect(screen.queryByText('Session Date')).not.toBeInTheDocument()

    // Click to open
    await user.click(screen.getByText('Session Info'))

    // Now Session Date field should be visible
    expect(screen.getByText('Session Date')).toBeInTheDocument()
  })

  it('shows extraction fields with confidence bars', async () => {
    const user = userEvent.setup()
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Risk Assessment')).toBeInTheDocument()
    })

    // Risk assessment is open by default — check confidence percentages
    // suicidalIdeation has confidence 0.65 = 65%, overallRiskLevel has 0.7 = 70%
    expect(screen.getByText('65%')).toBeInTheDocument()
    expect(screen.getByText('70%')).toBeInTheDocument()

    // Open session info to check another section
    await user.click(screen.getByText('Session Info'))
    expect(screen.getByText('98%')).toBeInTheDocument() // sessionDate confidence
  })

  it('ReviewActionPanel hidden when status is Approved', async () => {
    server.use(
      http.get('/api/review/session/:sessionId', () => {
        return HttpResponse.json(mockApprovedDetail)
      }),
    )

    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText(mockApprovedDetail.patientName)).toBeInTheDocument()
    })

    expect(screen.queryByText('Submit Review')).not.toBeInTheDocument()
  })

  it('Approve button disabled when reviewer name empty', async () => {
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Submit Review')).toBeInTheDocument()
    })

    const approveBtn = screen.getByRole('button', { name: 'Approve' })
    expect(approveBtn).toBeDisabled()
  })

  it('Approve button submits mutation', async () => {
    const user = userEvent.setup()
    let capturedBody: unknown = null

    server.use(
      http.post('/api/review/session/:sessionId', async ({ request }) => {
        capturedBody = await request.json()
        return new HttpResponse(null, { status: 200 })
      }),
    )

    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Submit Review')).toBeInTheDocument()
    })

    // Fill in reviewer name
    const nameInput = screen.getByPlaceholderText('Your name')
    await user.type(nameInput, 'Dr. Test')

    // Click approve
    const approveBtn = screen.getByRole('button', { name: 'Approve' })
    expect(approveBtn).toBeEnabled()
    await user.click(approveBtn)

    await waitFor(() => {
      expect(capturedBody).toEqual({
        action: 'Approved',
        reviewerName: 'Dr. Test',
      })
    })
  })

  it('shows error message on submission failure', async () => {
    const user = userEvent.setup()

    server.use(
      http.post('/api/review/session/:sessionId', () => {
        return new HttpResponse('Forbidden', { status: 403 })
      }),
    )

    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Submit Review')).toBeInTheDocument()
    })

    await user.type(screen.getByPlaceholderText('Your name'), 'Dr. Test')
    await user.click(screen.getByRole('button', { name: 'Approve' }))

    await waitFor(() => {
      expect(screen.getByText(/Failed:/)).toBeInTheDocument()
    })
  })

  it('shows review history entries', async () => {
    renderSessionDetail()

    await waitFor(() => {
      expect(screen.getByText('Review History')).toBeInTheDocument()
    })

    for (const rev of mockReviewDetail.reviews) {
      expect(screen.getByText(rev.reviewerName)).toBeInTheDocument()
      if (rev.notes) {
        expect(screen.getByText(rev.notes)).toBeInTheDocument()
      }
    }
  })
})
