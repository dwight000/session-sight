import type {
  ExtractionStep,
  ExtractionStepName,
  ExtractionStepStatus,
  ExtractionStepsResponse,
  ExtractionToolCall,
  ExtractionLlmTrace,
} from '../../types/extractionSteps'
import type { ExtractionResultResponse } from '../../types'

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
  documentStatus: 'Completed',
  steps: [
    makeStep('DocumentParse', 0, {
      durationMs: 1200,
      modelUsed: '',
      inputTokens: 0,
      outputTokens: 0,
      totalTokens: 0,
      resultSummaryJson: '{"pageCount":2,"fileSizeBytes":45056,"ocrConfidence":0.99}',
    }),
    makeStep('Intake', 1, {
      durationMs: 3400,
      modelUsed: 'gpt-4.1-nano',
      inputTokens: 400,
      outputTokens: 50,
      totalTokens: 450,
      resultSummaryJson: '{"isValid":true,"estimatedWordCount":607,"documentType":"progress"}',
    }),
    makeStep('ClinicalExtract', 2, {
      durationMs: 15000,
      inputTokens: 2000,
      outputTokens: 800,
      totalTokens: 2800,
      resultSummaryJson: '{"fieldCount":67,"overallConfidence":0.89,"toolCallCount":4}',
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
  documentStatus: 'Processing',
  steps: [
    mockExtractionStepsComplete.steps[0],
    mockExtractionStepsComplete.steps[1],
  ],
}

export const mockExtractionStepsFailed: ExtractionStepsResponse = {
  extractionId: 'ext-003',
  documentStatus: 'Failed',
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

export const mockExtractionResult: ExtractionResultResponse = {
  id: 'ext-001',
  sessionId: 'sess-001',
  data: {
    sessionInfo: {
      sessionDate: { value: '2025-01-15', confidence: 0.98, source: { text: 'Session date: January 15, 2025', startChar: 0, endChar: 30, section: 'header' } },
      sessionType: { value: 'Individual', confidence: 0.95, source: { text: 'Individual therapy session', startChar: 31, endChar: 57, section: 'header' } },
    },
    presentingConcerns: {
      primaryConcern: { value: 'Anxiety', confidence: 0.92, source: { text: 'Patient reports ongoing anxiety', startChar: 100, endChar: 131, section: 'presenting' } },
    },
    moodAssessment: {
      currentMood: { value: 'Anxious', confidence: 0.88, source: { text: 'Mood: anxious and restless', startChar: 200, endChar: 226, section: 'mood' } },
      phq9Score: { value: 12, confidence: 0.45, source: { text: 'PHQ-9 score estimated at 12', startChar: 227, endChar: 254, section: 'assessment' } },
    },
    riskAssessment: {
      suicidalIdeation: { value: 'Passive', confidence: 0.65, source: { text: 'Passive SI reported', startChar: 300, endChar: 319, section: 'risk' } },
      overallRiskLevel: { value: 'Moderate', confidence: 0.72, source: { text: 'Risk level: moderate', startChar: 320, endChar: 340, section: 'risk' } },
    },
    mentalStatusExam: {
      appearance: { value: 'Well-groomed', confidence: 0.9, source: { text: 'Well-groomed appearance', startChar: 400, endChar: 423, section: 'mse' } },
    },
    interventions: {
      techniquesUsed: { value: ['CBT', 'Breathing exercises'], confidence: 0.85, source: { text: 'Used CBT and breathing exercises', startChar: 500, endChar: 532, section: 'interventions' } },
    },
    diagnoses: {
      primaryDiagnosis: { value: 'GAD', confidence: 0.9, source: { text: 'Diagnosis: GAD', startChar: 600, endChar: 614, section: 'diagnosis' } },
    },
    treatmentProgress: {
      progressRating: { value: 'Moderate improvement', confidence: 0.78, source: { text: 'Moderate improvement noted', startChar: 700, endChar: 726, section: 'progress' } },
    },
    nextSteps: {
      followUpPlan: { value: 'Weekly sessions', confidence: 0.85, source: { text: 'Continue weekly sessions', startChar: 800, endChar: 824, section: 'plan' } },
    },
  },
  riskDiagnostics: {
    guardrailApplied: true,
    homicidalGuardrail: { applied: false, reason: '' },
    selfHarmGuardrail: { applied: true, reason: 'Passive SI detected in note text' },
    discrepancyCount: 2,
    fieldDecisions: [
      {
        field: 'suicidalIdeation',
        originalValue: 'None',
        reExtractedValue: 'Passive',
        finalValue: 'Passive',
        ruleApplied: 'ConservativeMerge',
        criteriaUsed: ['Higher severity wins', 'Source text mentions passive SI'],
        reasoningUsed: 'Re-extraction found passive SI that original extraction missed.',
      },
      {
        field: 'overallRiskLevel',
        originalValue: 'Low',
        reExtractedValue: 'Moderate',
        finalValue: 'Moderate',
        ruleApplied: 'ConservativeMerge',
        criteriaUsed: ['Higher severity wins'],
        reasoningUsed: 'Elevated due to passive SI finding.',
      },
    ],
  },
}

export const mockExtractionResultNoRisk: ExtractionResultResponse = {
  ...mockExtractionResult,
  riskDiagnostics: null,
}
