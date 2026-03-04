import { describe, it, expect } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { server } from '../../../src/test/mocks/server'
import { renderWithProviders } from '../../../src/test/render'
import { ExtractionPipelineView } from '../../../src/components/extraction/ExtractionPipelineView'
import {
  mockExtractionStepsPartial,
  mockExtractionStepsComplete,
  mockExtractionStepsWithDebate,
  makeDebateStep,
} from '../../../src/test/fixtures/extractionSteps'

describe('ExtractionPipelineView', () => {
  it('renders all 6 step names', async () => {
    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByText('Document Parse')).toBeInTheDocument()
    })
    expect(screen.getByText('Intake')).toBeInTheDocument()
    expect(screen.getByText('Clinical Extract')).toBeInTheDocument()
    expect(screen.getByText('Risk Assess')).toBeInTheDocument()
    expect(screen.getByText('Summarize')).toBeInTheDocument()
    expect(screen.getByText('Search Index')).toBeInTheDocument()
  })

  it('shows "not available" for 404 in historical mode', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return new HttpResponse('API 404: Not Found', { status: 404 })
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="old-sess" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByText('Processing details not available.')).toBeInTheDocument()
    })
  })

  it('shows Running indicator for current step in live mode with partial data', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsPartial)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={true} />)

    await waitFor(() => {
      expect(screen.getByText('Running...')).toBeInTheDocument()
    })
  })

  it('shows progress label in live mode with partial data', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsPartial)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={true} />)

    await waitFor(() => {
      expect(screen.getByText(/2\/6/)).toBeInTheDocument()
    })
  })

  it('shows pipeline totals banner for completed pipeline', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsComplete)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByText(/6 steps/)).toBeInTheDocument()
    })
    expect(screen.getByText(/tokens/)).toBeInTheDocument()
  })

  it('shows Gantt bar for completed pipeline', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsComplete)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByRole('img', { name: 'Pipeline duration chart' })).toBeInTheDocument()
    })
  })

  it('does not show pipeline banner in live mode', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsComplete)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-001" isLive={true} />)

    await waitFor(() => {
      expect(screen.getByText('Document Parse')).toBeInTheDocument()
    })
    expect(screen.queryByText(/6 steps/)).not.toBeInTheDocument()
  })

  it('renders all 7 step names when debate is present', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsWithDebate)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-debate" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByText('Risk Debate')).toBeInTheDocument()
    })
    expect(screen.getByText('Document Parse')).toBeInTheDocument()
    expect(screen.getByText('Summarize')).toBeInTheDocument()
    expect(screen.getByText('Search Index')).toBeInTheDocument()
  })

  it('shows "7 steps" in pipeline totals banner with debate', async () => {
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(mockExtractionStepsWithDebate)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-debate-banner" isLive={false} />)

    await waitFor(() => {
      expect(screen.getByText(/7 steps/)).toBeInTheDocument()
    })
    expect(screen.getByText(/tokens/)).toBeInTheDocument()
  })

  it('shows correct progress denominator with debate in live mode', async () => {
    const partialWithDebate = {
      extractionId: 'ext-006',
      documentStatus: 'Processing',
      failureKind: null,
      errorMessage: null,
      steps: [
        mockExtractionStepsComplete.steps[0], // DocumentParse
        mockExtractionStepsComplete.steps[1], // Intake
        mockExtractionStepsComplete.steps[2], // ClinicalExtract
        mockExtractionStepsComplete.steps[3], // RiskAssess
        makeDebateStep(),                     // RiskDebate (optional)
      ],
    }
    server.use(
      http.get('/api/sessions/:sessionId/extraction/steps', () => {
        return HttpResponse.json(partialWithDebate)
      }),
    )

    renderWithProviders(<ExtractionPipelineView sessionId="sess-debate-live" isLive={true} />)

    await waitFor(() => {
      expect(screen.getByText(/\/7/)).toBeInTheDocument()
    })
  })
})
