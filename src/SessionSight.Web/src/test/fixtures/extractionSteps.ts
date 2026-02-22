import type {
  ExtractionStep,
  ExtractionStepName,
  ExtractionStepStatus,
  ExtractionStepsResponse,
  ExtractionToolCall,
  ExtractionLlmTrace,
} from '../../types/extractionSteps'

let idCounter = 0

export function makeStep(
  stepName: ExtractionStepName,
  stepOrder: number,
  overrides: Partial<ExtractionStep> = {},
): ExtractionStep {
  idCounter++
  return {
    id: `step-${idCounter}`,
    stepName,
    status: 'Succeeded' as ExtractionStepStatus,
    stepOrder,
    startedAt: '2025-06-01T10:00:00Z',
    completedAt: '2025-06-01T10:00:07Z',
    durationMs: 7000,
    modelUsed: 'gpt-4.1-mini',
    inputTokens: 200,
    outputTokens: 100,
    totalTokens: 300,
    resultSummaryJson: null,
    errorMessage: null,
    toolCalls: [],
    llmTraces: [],
    ...overrides,
  }
}

export const mockToolCall: ExtractionToolCall = {
  toolName: 'ExtractMoodTool',
  loopRound: 1,
  succeeded: true,
  durationMs: 450,
  calledAt: '2025-06-01T10:00:03Z',
  inputJson: '{"section":"mood"}',
  outputJson: '{"mood":"euthymic"}',
}

export const mockLlmTrace: ExtractionLlmTrace = {
  modelUsed: 'gpt-4.1-mini',
  loopRound: 1,
  inputTokens: 150,
  outputTokens: 80,
  totalTokens: 230,
  durationMs: 2100,
  promptText: 'Extract clinical fields from the following note...',
  responseText: '{"mood":"euthymic","affect":"congruent"}',
  calledAt: '2025-06-01T10:00:02Z',
}

export const mockExtractionStepsComplete: ExtractionStepsResponse = {
  extractionId: 'ext-001',
  steps: [
    makeStep('DocumentParse', 0, {
      durationMs: 1200,
      modelUsed: '',
      inputTokens: 0,
      outputTokens: 0,
      totalTokens: 0,
      resultSummaryJson: '{"pageCount":2,"fileSizeKb":44,"ocrConfidence":0.99}',
    }),
    makeStep('Intake', 1, {
      durationMs: 3400,
      modelUsed: 'gpt-4.1-nano',
      inputTokens: 400,
      outputTokens: 50,
      totalTokens: 450,
      resultSummaryJson: '{"isValidSessionNote":true,"wordCount":607,"noteType":"progress"}',
    }),
    makeStep('ClinicalExtract', 2, {
      durationMs: 15000,
      inputTokens: 2000,
      outputTokens: 800,
      totalTokens: 2800,
      resultSummaryJson: '{"fieldCount":67,"averageConfidence":0.89,"toolCallCount":4}',
      toolCalls: [mockToolCall],
      llmTraces: [mockLlmTrace],
    }),
    makeStep('RiskAssess', 3, {
      durationMs: 4500,
      inputTokens: 600,
      outputTokens: 150,
      totalTokens: 750,
      resultSummaryJson: '{"riskLevel":"Low","requiresReview":false}',
    }),
    makeStep('Summarize', 4, {
      durationMs: 5200,
      modelUsed: 'gpt-4.1-nano',
      inputTokens: 800,
      outputTokens: 200,
      totalTokens: 1000,
      resultSummaryJson: '{"oneLiner":"Patient shows improved mood with consistent CBT engagement"}',
    }),
    makeStep('SearchIndex', 5, {
      durationMs: 800,
      modelUsed: '',
      inputTokens: 0,
      outputTokens: 0,
      totalTokens: 0,
      resultSummaryJson: '{"indexed":true,"error":null}',
    }),
  ],
}

export const mockExtractionStepsPartial: ExtractionStepsResponse = {
  extractionId: 'ext-002',
  steps: [
    mockExtractionStepsComplete.steps[0],
    mockExtractionStepsComplete.steps[1],
  ],
}

export const mockExtractionStepsFailed: ExtractionStepsResponse = {
  extractionId: 'ext-003',
  steps: [
    mockExtractionStepsComplete.steps[0],
    mockExtractionStepsComplete.steps[1],
    makeStep('ClinicalExtract', 2, {
      status: 'Failed',
      durationMs: 8000,
      errorMessage: 'Azure OpenAI rate limit exceeded',
      resultSummaryJson: null,
    }),
  ],
}
