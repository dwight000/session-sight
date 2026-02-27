import { describe, it, expect } from 'vitest'
import { getSegments, parsePromptText } from '../../../src/components/extraction/promptParser'
import type { ExtractionLlmTrace } from '../../../src/types/extractionSteps'

describe('getSegments', () => {
  it('parses promptSegmentsJson when available', () => {
    const trace: ExtractionLlmTrace = {
      modelUsed: 'gpt-4.1-mini',
      loopRound: 0,
      inputTokens: 100,
      outputTokens: 50,
      totalTokens: 150,
      durationMs: 500,
      promptText: null,
      promptSegmentsJson: JSON.stringify([
        { role: 'system', content: 'You are an assistant' },
        { role: 'user', content: 'Extract fields' },
      ]),
      responseText: null,
      calledAt: '2025-01-01T00:00:00Z',
    }

    const segments = getSegments(trace)
    expect(segments).toHaveLength(2)
    expect(segments[0]).toEqual({ role: 'system', content: 'You are an assistant' })
    expect(segments[1]).toEqual({ role: 'user', content: 'Extract fields' })
  })

  it('falls back to parsePromptText when only promptText is set', () => {
    const trace: ExtractionLlmTrace = {
      modelUsed: 'gpt-4.1-mini',
      loopRound: 0,
      inputTokens: 100,
      outputTokens: 50,
      totalTokens: 150,
      durationMs: 500,
      promptText: '[SYSTEM]\nYou are an assistant\n---\n[USER]\nExtract fields',
      promptSegmentsJson: null,
      responseText: null,
      calledAt: '2025-01-01T00:00:00Z',
    }

    const segments = getSegments(trace)
    expect(segments).toHaveLength(2)
    expect(segments[0].role).toBe('system')
    expect(segments[0].content).toBe('You are an assistant')
    expect(segments[1].role).toBe('user')
    expect(segments[1].content).toBe('Extract fields')
  })

  it('returns empty array when neither field is set', () => {
    const trace: ExtractionLlmTrace = {
      modelUsed: 'gpt-4.1-mini',
      loopRound: 0,
      inputTokens: 0,
      outputTokens: 0,
      totalTokens: 0,
      durationMs: 0,
      promptText: null,
      promptSegmentsJson: null,
      responseText: null,
      calledAt: '2025-01-01T00:00:00Z',
    }

    expect(getSegments(trace)).toEqual([])
  })

  it('returns empty array for invalid JSON in promptSegmentsJson', () => {
    const trace: ExtractionLlmTrace = {
      modelUsed: 'gpt-4.1-mini',
      loopRound: 0,
      inputTokens: 0,
      outputTokens: 0,
      totalTokens: 0,
      durationMs: 0,
      promptText: null,
      promptSegmentsJson: 'not valid json',
      responseText: null,
      calledAt: '2025-01-01T00:00:00Z',
    }

    expect(getSegments(trace)).toEqual([])
  })
})

describe('parsePromptText', () => {
  it('parses system, user, assistant, and tool segments', () => {
    const text = [
      '[SYSTEM]\nYou are a clinical assistant',
      '[USER]\nExtract from note',
      '[ASSISTANT]\nHere is the extraction\n[Tool Calls: check_risk, lookup_code]',
      '[TOOL]\n{"result":"ok"}',
    ].join('\n---\n')

    const segments = parsePromptText(text)
    expect(segments).toHaveLength(4)
    expect(segments[0]).toEqual({ role: 'system', content: 'You are a clinical assistant' })
    expect(segments[1]).toEqual({ role: 'user', content: 'Extract from note' })
    expect(segments[2]).toEqual({ role: 'assistant', content: 'Here is the extraction' })
    expect(segments[3]).toEqual({ role: 'tool', content: '{"result":"ok"}' })
  })

  it('handles empty text', () => {
    expect(parsePromptText('')).toEqual([])
  })

  it('handles text without recognized role prefixes', () => {
    expect(parsePromptText('just some random text')).toEqual([])
  })
})
