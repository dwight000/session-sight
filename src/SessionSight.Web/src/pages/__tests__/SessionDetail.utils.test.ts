import { describe, it, expect } from 'vitest'
import { formatFieldValue, formatFieldName, isExtractedField } from '../../utils/format'

describe('formatFieldValue', () => {
  it('returns em-dash for null', () => {
    expect(formatFieldValue(null)).toBe('\u2014')
  })

  it('returns em-dash for undefined', () => {
    expect(formatFieldValue(undefined)).toBe('\u2014')
  })

  it('returns em-dash for empty array', () => {
    expect(formatFieldValue([])).toBe('\u2014')
  })

  it('joins non-empty array with commas', () => {
    expect(formatFieldValue(['CBT', 'DBT'])).toBe('CBT, DBT')
  })

  it('stringifies objects', () => {
    expect(formatFieldValue({ a: 1 })).toBe('{"a":1}')
  })

  it('converts strings as-is', () => {
    expect(formatFieldValue('hello')).toBe('hello')
  })

  it('converts numbers to string', () => {
    expect(formatFieldValue(42)).toBe('42')
  })

  it('converts booleans to string', () => {
    expect(formatFieldValue(true)).toBe('true')
  })
})

describe('formatFieldName', () => {
  it('converts camelCase to title case', () => {
    expect(formatFieldName('primaryConcern')).toBe('Primary Concern')
  })

  it('handles single word', () => {
    expect(formatFieldName('mood')).toBe('Mood')
  })

  it('handles multiple capitals', () => {
    expect(formatFieldName('phq9Score')).toBe('Phq9 Score')
  })

  it('handles already capitalized first letter', () => {
    expect(formatFieldName('SessionDate')).toBe('Session Date')
  })
})

describe('isExtractedField', () => {
  it('returns true for valid extracted field', () => {
    expect(isExtractedField({ value: 'test', confidence: 0.9, source: 'note' })).toBe(true)
  })

  it('returns true with just value and confidence', () => {
    expect(isExtractedField({ value: null, confidence: 0 })).toBe(true)
  })

  it('returns false for null', () => {
    expect(isExtractedField(null)).toBe(false)
  })

  it('returns false for string', () => {
    expect(isExtractedField('test')).toBe(false)
  })

  it('returns false for object missing confidence', () => {
    expect(isExtractedField({ value: 'test' })).toBe(false)
  })

  it('returns false for object missing value', () => {
    expect(isExtractedField({ confidence: 0.5 })).toBe(false)
  })
})
