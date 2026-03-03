import type {
  ExtractionStepName,
  DocumentParseResult,
  IntakeResult,
  ClinicalExtractResult,
  RiskAssessResult,
  DebateResultSummary,
  SummarizeResult,
  SearchIndexResult,
} from '../../types/extractionSteps'

export const STEP_ORDER: ExtractionStepName[] = [
  'DocumentParse',
  'Intake',
  'ClinicalExtract',
  'RiskAssess',
  'Summarize',
  'SearchIndex',
]

export const STEP_DISPLAY_NAMES: Record<ExtractionStepName, string> = {
  DocumentParse: 'Document Parse',
  Intake: 'Intake',
  ClinicalExtract: 'Clinical Extract',
  RiskAssess: 'Risk Assess',
  RiskDebate: 'Risk Debate',
  Summarize: 'Summarize',
  SearchIndex: 'Search Index',
}

// Per 1M tokens: [input, output]
const MODEL_PRICING: Record<string, [number, number]> = {
  'gpt-4.1-mini': [0.4, 1.6],
  'gpt-4.1-nano': [0.1, 0.4],
  'Mistral-Large-3': [0.5, 1.5],
}

export function estimateCost(model: string, inputTokens: number, outputTokens: number): number | null {
  const pricing = MODEL_PRICING[model]
  if (!pricing) return null
  const [inputRate, outputRate] = pricing
  return (inputTokens / 1_000_000) * inputRate + (outputTokens / 1_000_000) * outputRate
}

export function formatDurationMs(ms: number): string {
  if (ms === 0) return '<1ms'
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`
  return `${ms}ms`
}

export function formatResultSummary(stepName: ExtractionStepName, json: string | null): string | null {
  if (!json) return null
  try {
    switch (stepName) {
      case 'DocumentParse': {
        const r = JSON.parse(json) as DocumentParseResult
        const kb = Math.round(r.fileSizeBytes / 1024)
        return `${r.pageCount} pages, ${kb} KB, OCR ${Math.round(r.ocrConfidence * 100)}%`
      }
      case 'Intake': {
        const r = JSON.parse(json) as IntakeResult
        const validity = r.isValid ? 'Valid Session Note' : 'Invalid'
        const parts = [validity]
        if (r.therapistName) parts.push(r.therapistName)
        if (r.sessionDate) parts.push(r.sessionDate)
        parts.push(`${r.estimatedWordCount} words`)
        return parts.join(' \u00B7 ')
      }
      case 'ClinicalExtract': {
        const r = JSON.parse(json) as ClinicalExtractResult
        return `${r.fieldCount} fields, ${Math.round(r.overallConfidence * 100)}% confidence, ${r.toolCallCount} tool calls`
      }
      case 'RiskAssess': {
        const r = JSON.parse(json) as RiskAssessResult
        return r.requiresReview ? `${r.riskLevel} — requires review` : r.riskLevel
      }
      case 'RiskDebate': {
        const r = JSON.parse(json) as DebateResultSummary
        return `Verdict: ${r.finalRiskLevel} (${Math.round(r.finalConfidence * 100)}% confidence, ${r.rounds.length} round${r.rounds.length !== 1 ? 's' : ''})`
      }
      case 'Summarize': {
        const r = JSON.parse(json) as SummarizeResult
        return r.oneLiner
      }
      case 'SearchIndex': {
        const r = JSON.parse(json) as SearchIndexResult
        return r.indexed ? 'Indexed successfully' : `Not indexed: ${r.error}`
      }
    }
  } catch {
    return null
  }
}
