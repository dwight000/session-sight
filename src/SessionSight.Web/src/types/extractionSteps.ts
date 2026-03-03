export type ExtractionStepStatus = 'Running' | 'Succeeded' | 'Failed' | 'Skipped'

export type StepViewMode = 'raw' | 'conversation' | 'activity' | 'summary'

export interface PromptSegment {
  role: 'system' | 'user' | 'assistant' | 'tool'
  content: string
  toolCalls?: string | null
}

export type ExtractionStepName =
  | 'DocumentParse'
  | 'Intake'
  | 'ClinicalExtract'
  | 'RiskAssess'
  | 'RiskDebate'
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
  promptSegmentsJson: string | null
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
  documentStatus: string | null
  failureKind: string | null
  errorMessage: string | null
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
  sessionDate?: string
  language?: string
  therapistName?: string
  patientId?: string
}

export interface ClinicalExtractResult {
  fieldCount: number
  overallConfidence: number
  toolCallCount: number
  lowConfidenceFields?: string[]
  errors?: string[]
}

export interface RiskAssessResult {
  riskLevel: string
  requiresReview: boolean
  discrepancyCount?: number
  guardrailApplied?: boolean
  reviewReasons?: string[]
  fieldDecisions?: RiskFieldDecisionSummary[]
  keywordMatches?: string[]
  suicidalGuardrailApplied?: boolean
  homicidalGuardrailApplied?: boolean
  contentFilterBlocked?: boolean
}

export interface RiskFieldDecisionSummary {
  field: string
  ruleApplied: string
}

export interface SummarizeResult {
  oneLiner: string
  interventionsUsed?: string[]
  keyPoints?: string
  nextSessionFocus?: string
  riskLevel?: string
}

export interface SearchIndexResult {
  indexed: boolean
  error: string | null
}

export interface DebateRound {
  roundNumber: number
  advocateArgument: string
  challengerArgument: string
}

export interface DebateResultSummary {
  finalRiskLevel: string
  finalConfidence: number
  requiresReview: boolean
  reviewReasons: string[]
  advocateModel: string
  challengerModel: string
  judgeModel: string
  rounds: DebateRound[]
  judgeSynthesis: string
}
