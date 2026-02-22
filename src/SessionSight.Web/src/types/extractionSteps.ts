export type ExtractionStepStatus = 'Running' | 'Succeeded' | 'Failed' | 'Skipped'

export type ExtractionStepName =
  | 'DocumentParse'
  | 'Intake'
  | 'ClinicalExtract'
  | 'RiskAssess'
  | 'Summarize'
  | 'SearchIndex'

export interface ExtractionToolCall {
  toolName: string
  loopRound: number
  succeeded: boolean
  durationMs: number
  calledAt: string
  inputJson: string | null
  outputJson: string | null
}

export interface ExtractionLlmTrace {
  modelUsed: string
  loopRound: number
  inputTokens: number
  outputTokens: number
  totalTokens: number
  durationMs: number
  promptText: string | null
  responseText: string | null
  calledAt: string
}

export interface ExtractionStep {
  id: string
  stepName: ExtractionStepName
  status: ExtractionStepStatus
  stepOrder: number
  startedAt: string
  completedAt: string | null
  durationMs: number
  modelUsed: string
  inputTokens: number
  outputTokens: number
  totalTokens: number
  resultSummaryJson: string | null
  errorMessage: string | null
  toolCalls: ExtractionToolCall[]
  llmTraces: ExtractionLlmTrace[]
}

export interface ExtractionStepsResponse {
  extractionId: string
  steps: ExtractionStep[]
}

// Per-step result summary shapes (parsed from resultSummaryJson)
export interface DocumentParseResult {
  pageCount: number
  fileSizeBytes: number
  ocrConfidence: number
}

export interface IntakeResult {
  isValid: boolean
  estimatedWordCount: number
  documentType: string
}

export interface ClinicalExtractResult {
  fieldCount: number
  overallConfidence: number
  toolCallCount: number
}

export interface RiskAssessResult {
  riskLevel: string
  requiresReview: boolean
}

export interface SummarizeResult {
  oneLiner: string
}

export interface SearchIndexResult {
  indexed: boolean
  error: string | null
}
