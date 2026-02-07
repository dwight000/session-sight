import type { ExtractedField } from '../types'

export function formatFieldValue(val: unknown): string {
  if (val === null || val === undefined) return '\u2014'
  if (Array.isArray(val)) return val.length > 0 ? val.join(', ') : '\u2014'
  if (typeof val === 'object') return JSON.stringify(val)
  return String(val)
}

export function formatFieldName(key: string): string {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (s) => s.toUpperCase())
    .trim()
}

export function isExtractedField(obj: unknown): obj is ExtractedField {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    'value' in obj &&
    'confidence' in obj
  )
}
