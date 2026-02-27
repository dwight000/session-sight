import type { ExtractionLlmTrace, PromptSegment } from '../../types/extractionSteps'

/**
 * Get structured segments for a trace. Uses promptSegmentsJson (new data)
 * or falls back to parsing promptText (old data).
 */
export function getSegments(trace: ExtractionLlmTrace): PromptSegment[] {
  if (trace.promptSegmentsJson) {
    try {
      return JSON.parse(trace.promptSegmentsJson) as PromptSegment[]
    } catch {
      return []
    }
  }
  if (trace.promptText) {
    return parsePromptText(trace.promptText)
  }
  return []
}

/**
 * Fallback parser for old-format promptText.
 * Splits on "\n---\n", matches [SYSTEM]/[USER]/[ASSISTANT]/[TOOL] prefix,
 * strips prefix + leading newline, returns PromptSegment[].
 * For [ASSISTANT] segments, detects "\n[Tool Calls: A, B]" suffix —
 * strips it from content but does NOT include in output (tool calls
 * are already in the separate toolCalls array).
 */
export function parsePromptText(promptText: string): PromptSegment[] {
  const parts = promptText.split('\n---\n')
  const segments: PromptSegment[] = []

  for (const part of parts) {
    const trimmed = part.trim()
    if (!trimmed) continue

    const roleMatch = trimmed.match(/^\[(SYSTEM|USER|ASSISTANT|TOOL)\]\n?/)
    if (!roleMatch) continue

    const role = roleMatch[1].toLowerCase() as PromptSegment['role']
    let content = trimmed.slice(roleMatch[0].length)

    // Strip [Tool Calls: ...] suffix from assistant messages
    if (role === 'assistant') {
      content = content.replace(/\n\[Tool Calls: [^\]]+\]$/, '')
    }

    segments.push({ role, content })
  }

  return segments
}
