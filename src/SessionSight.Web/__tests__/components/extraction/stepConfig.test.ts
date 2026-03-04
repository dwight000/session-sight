import { describe, it, expect } from 'vitest'
import { formatDurationMs, formatResultSummary, estimateCost, STEP_ORDER, DISPLAY_ORDER } from '../../../src/components/extraction/stepConfig'

describe('formatDurationMs', () => {
  it('returns <1ms for 0', () => {
    expect(formatDurationMs(0)).toBe('<1ms')
  })

  it('returns ms for values under 1000', () => {
    expect(formatDurationMs(450)).toBe('450ms')
  })

  it('returns seconds for values >= 1000', () => {
    expect(formatDurationMs(1200)).toBe('1.2s')
  })
})

describe('formatResultSummary', () => {
  it('returns null for null json', () => {
    expect(formatResultSummary('Intake', null)).toBeNull()
  })

  it('formats Intake with therapist and date', () => {
    const json = '{"isValid":true,"estimatedWordCount":607,"documentType":"progress","therapistName":"Dr. Torres","sessionDate":"2025-06-01"}'
    const result = formatResultSummary('Intake', json)
    expect(result).toContain('Valid Session Note')
    expect(result).toContain('Dr. Torres')
    expect(result).toContain('2025-06-01')
    expect(result).toContain('607 words')
    expect(result).toContain('\u00B7')
  })

  it('formats Intake without therapist/date gracefully', () => {
    const json = '{"isValid":true,"estimatedWordCount":100,"documentType":"progress"}'
    const result = formatResultSummary('Intake', json)
    expect(result).toBe('Valid Session Note \u00B7 100 words')
  })

  it('formats DocumentParse', () => {
    const json = '{"pageCount":2,"fileSizeBytes":45056,"ocrConfidence":0.99}'
    expect(formatResultSummary('DocumentParse', json)).toBe('2 pages, 44 KB, OCR 99%')
  })

  it('formats RiskAssess', () => {
    const json = '{"riskLevel":"Low","requiresReview":false}'
    expect(formatResultSummary('RiskAssess', json)).toBe('Low')
  })

  it('formats SearchIndex', () => {
    const json = '{"indexed":true,"error":null}'
    expect(formatResultSummary('SearchIndex', json)).toBe('Indexed successfully')
  })
})

describe('DISPLAY_ORDER', () => {
  it('contains all STEP_ORDER entries plus RiskDebate', () => {
    for (const step of STEP_ORDER) {
      expect(DISPLAY_ORDER).toContain(step)
    }
    expect(DISPLAY_ORDER).toContain('RiskDebate')
    expect(DISPLAY_ORDER).toHaveLength(STEP_ORDER.length + 1)
  })

  it('places RiskDebate after RiskAssess', () => {
    const riskAssessIdx = DISPLAY_ORDER.indexOf('RiskAssess')
    const riskDebateIdx = DISPLAY_ORDER.indexOf('RiskDebate')
    expect(riskDebateIdx).toBe(riskAssessIdx + 1)
  })
})

describe('estimateCost', () => {
  it('returns cost for known model', () => {
    const cost = estimateCost('gpt-4.1-mini', 1000, 500)
    expect(cost).toBeCloseTo(0.0012, 4)
  })

  it('returns null for unknown model', () => {
    expect(estimateCost('unknown', 1000, 500)).toBeNull()
  })
})
