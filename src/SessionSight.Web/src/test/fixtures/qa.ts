import type { QASourceCitation, QAResponse } from '../../types'

export const mockQASource: QASourceCitation = {
  sessionId: 's1',
  sessionDate: '2026-01-15T00:00:00+00:00',
  sessionType: 'Individual',
  summary: 'Patient discussed anxiety and sleep issues.',
  relevanceScore: 0.92,
}

export const mockQAResponse: QAResponse = {
  question: 'What were the main concerns?',
  answer: 'The patient discussed anxiety and sleep issues.',
  sources: [mockQASource],
  confidence: 0.85,
  modelUsed: 'gpt-4.1-nano',
  warning: null,
  toolCallCount: 0,
  generatedAt: '2026-02-19T10:30:00Z',
}

export const mockQAResponseWithWarning: QAResponse = {
  ...mockQAResponse,
  warning: 'More than 10 sessions matched — results may be truncated.',
}

export const mockQAResponseAgentic: QAResponse = {
  ...mockQAResponse,
  toolCallCount: 3,
  sources: [
    {
      sessionId: 's1',
      sessionDate: '',
      sessionType: null,
      summary: null,
      relevanceScore: 0,
    },
  ],
}

export const mockQAResponseLowConfidence: QAResponse = {
  ...mockQAResponse,
  confidence: 0,
  answer: 'An error occurred while processing your question.',
}
