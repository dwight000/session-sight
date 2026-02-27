import { useState } from 'react'
import type { ExtractionToolCall, ExtractionLlmTrace } from '../../types/extractionSteps'
import { buildTimeline } from './mergeTimeline'
import { estimateCost, formatDurationMs } from './stepConfig'

interface ActivityViewProps {
  toolCalls: ExtractionToolCall[]
  traces: ExtractionLlmTrace[]
  defaultOpen?: boolean
}

interface TimelineEvent {
  id: string
  type: 'system' | 'note' | 'llm' | 'tool' | 'complete'
  title: string
  subtitle?: string
  description?: string
  phase?: string
  isFinal?: boolean
  responsePreview?: string
  noTextResponse?: boolean
  validationNote?: string
  timestamp?: string
  duration?: string
  details?: string
  toolCalls?: ExtractionToolCall[]
  dotClass: string
  dotSize?: string
}

function formatTime12h(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit', second: '2-digit', hour12: true })
}

const TOOL_DESCRIPTIONS: Record<string, string> = {
  check_risk_keywords: 'scanning for risk keywords',
  lookup_diagnosis_code: 'validating diagnosis codes',
  validate_and_score: 'validating extraction against schema',
}

function getToolSummary(tc: ExtractionToolCall): string {
  if (!tc.outputJson) return ''
  try {
    const out = JSON.parse(tc.outputJson)
    switch (tc.toolName) {
      case 'check_risk_keywords': {
        const matches = out.SuicidalMatches || out.HomicidalMatches || out.SelfHarmMatches || []
        return matches.length > 0 ? `Found: ${matches.join(', ')}` : 'No matches'
      }
      case 'lookup_diagnosis_code':
        return `${out.Code ?? '?'} \u2192 ${out.IsValid ? 'Valid' : 'Invalid'}${out.Description ? ` (${out.Description})` : ''}`
      case 'validate_and_score':
        return (out.Errors?.length ?? 0) === 0 ? 'Passed' : `Failed: ${out.Errors?.join(', ')}`
      default:
        return tc.outputJson.length > 80 ? tc.outputJson.slice(0, 80) + '\u2026' : tc.outputJson
    }
  } catch {
    return tc.outputJson.length > 80 ? tc.outputJson.slice(0, 80) + '\u2026' : tc.outputJson
  }
}

export function ActivityView({ toolCalls, traces, defaultOpen }: ActivityViewProps) {
  const timeline = buildTimeline(toolCalls, traces)
  // Track manual overrides instead of open IDs.
  // When defaultOpen=true, everything is expanded by default — overrides = collapsed by user.
  // When defaultOpen=false, everything is collapsed — overrides = opened by user.
  // This handles live data where new events arrive after mount.
  const [manualOverrides, setManualOverrides] = useState<Set<string>>(new Set())

  if (timeline.length === 0 && toolCalls.length === 0) return null

  const isEventOpen = (id: string, expandable: boolean) => {
    if (!expandable) return false
    return defaultOpen ? !manualOverrides.has(id) : manualOverrides.has(id)
  }

  const toggle = (id: string) => {
    setManualOverrides((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  // Build flat event list
  const events: TimelineEvent[] = []

  // Pre-compute round metadata for phase labels and Final badge
  const firstResponseRound = timeline.find((r) => r.responseText)?.round ?? -1
  const lastResponseRound = [...timeline].reverse().find((r) => r.responseText)?.round ?? -1

  for (const round of timeline) {
    // First round: system prompt + note
    if (round.round === 0) {
      const systemSeg = round.segments.find((s) => s.role === 'system')
      if (systemSeg) {
        events.push({
          id: 'system',
          type: 'system',
          title: 'System prompt loaded',
          description: 'AI role and instructions configured',
          timestamp: round.llmTrace ? formatTime12h(round.llmTrace.calledAt) : undefined,
          details: systemSeg.content,
          dotClass: 'border-2 border-gray-300 bg-white',
        })
      }
      const userSeg = round.segments.find((s) => s.role === 'user')
      if (userSeg) {
        const wordCount = userSeg.content.split(/\s+/).length
        events.push({
          id: 'note',
          type: 'note',
          title: `Therapy note submitted (${wordCount} words)`,
          subtitle: userSeg.content.slice(0, 100) + (userSeg.content.length > 100 ? '\u2026' : ''),
          timestamp: round.llmTrace ? formatTime12h(round.llmTrace.calledAt) : undefined,
          details: userSeg.content,
          dotClass: 'border-2 border-gray-300 bg-white',
        })
      }
    }

    // Tool results returned to AI (from previous round's tool execution)
    const toolSegments = round.segments.filter((s) => s.role === 'tool')
    if (toolSegments.length > 0) {
      events.push({
        id: `tool-results-${round.round}`,
        type: 'system',
        title: `${toolSegments.length} tool result${toolSegments.length !== 1 ? 's' : ''} returned to AI`,
        description: 'AI reviewing tool feedback before next round',
        timestamp: round.llmTrace ? formatTime12h(round.llmTrace.calledAt) : undefined,
        details: toolSegments.map((s) => s.content).join('\n\n'),
        dotClass: 'border-2 border-green-400 bg-white',
      })
    }

    // LLM call event
    if (round.llmTrace) {
      const displayRound = round.round + 1
      const toolCount = round.toolCalls.length
      const isToolOnly = !round.responseText && toolCount > 0
      const toolLabel = isToolOnly
        ? ` \u2192 called ${toolCount} tool${toolCount !== 1 ? 's' : ''}`
        : ''

      let llmDescription: string | undefined
      if (isToolOnly) {
        const descs = round.toolCalls
          .map((tc) => TOOL_DESCRIPTIONS[tc.toolName])
          .filter(Boolean)
        if (descs.length > 0) {
          const joined = descs.join(', ')
          llmDescription = joined.charAt(0).toUpperCase() + joined.slice(1)
        }
      } else if (round.round === 0) {
        llmDescription = 'AI reading note and generating initial extraction'
      } else {
        llmDescription = 'AI refining extraction based on tool feedback'
      }

      // Phase label
      let phase: string | undefined
      if (isToolOnly) {
        phase = 'Gathering Data'
      } else if (round.round === firstResponseRound) {
        phase = 'Extraction'
      } else {
        phase = 'Refinement'
      }

      // Final badge: last round that produced a text response
      const isFinal = !isToolOnly && round.round === lastResponseRound

      // Response preview (collapsed view of the response box)
      const responsePreview = round.responseText
        ? round.responseText.slice(0, 80) + (round.responseText.length > 80 ? '\u2026' : '')
        : undefined

      // Validation-aware callout: check previous round for validate_and_score errors
      let validationNote: string | undefined
      if (!isToolOnly && round.round > 0) {
        const prevRound = timeline.find((r) => r.round === round.round - 1)
        const validateCall = prevRound?.toolCalls.find((tc) => tc.toolName === 'validate_and_score')
        if (validateCall?.outputJson) {
          try {
            const out = JSON.parse(validateCall.outputJson)
            const errorCount = out.Errors?.length ?? 0
            if (errorCount > 0) {
              validationNote = `Refined after validation found ${errorCount} issue${errorCount !== 1 ? 's' : ''}`
            }
          } catch { /* skip */ }
        }
      }

      events.push({
        id: `llm-${round.round}`,
        type: 'llm',
        title: isToolOnly
          ? `AI responded with ${toolCount} tool call${toolCount !== 1 ? 's' : ''} (Round ${displayRound})`
          : `LLM call (Round ${displayRound})${toolLabel}`,
        subtitle: `${round.llmTrace.modelUsed} \u00B7 ${round.inputTokens} in / ${round.outputTokens} out`,
        description: llmDescription,
        timestamp: formatTime12h(round.llmTrace.calledAt),
        phase,
        isFinal,
        responsePreview,
        noTextResponse: isToolOnly,
        validationNote,
        duration: formatDurationMs(round.durationMs),
        details: round.responseText || undefined,
        toolCalls: !round.responseText && toolCount > 0 ? round.toolCalls : undefined,
        dotClass: 'bg-blue-500',
      })
    }

    // Tool call events (skip if already nested inside the LLM event above)
    if (round.responseText || !round.llmTrace)
    for (let j = 0; j < round.toolCalls.length; j++) {
      const tc = round.toolCalls[j]
      events.push({
        id: `tool-${round.round}-${j}-${tc.toolName}`,
        type: 'tool',
        title: tc.toolName,
        subtitle: getToolSummary(tc),
        timestamp: formatTime12h(tc.calledAt),
        duration: formatDurationMs(tc.durationMs),
        details: [
          tc.inputJson ? `Input: ${tc.inputJson}` : null,
          tc.outputJson ? `Output: ${tc.outputJson}` : null,
        ]
          .filter(Boolean)
          .join('\n\n'),
        dotClass: tc.succeeded ? 'bg-green-500' : 'bg-red-500',
      })
    }
  }

  // Complete footer
  const totalDurationMs = traces.reduce((sum, t) => sum + t.durationMs, 0)
  const totalIn = traces.reduce((sum, t) => sum + t.inputTokens, 0)
  const totalOut = traces.reduce((sum, t) => sum + t.outputTokens, 0)
  const model = traces[0]?.modelUsed ?? ''
  const cost = estimateCost(model, totalIn, totalOut)

  events.push({
    id: 'complete',
    type: 'complete',
    title: 'Complete',
    duration: formatDurationMs(totalDurationMs),
    subtitle: cost !== null ? `$${cost.toFixed(4)}` : undefined,
    dotClass: 'bg-green-500',
    dotSize: 'h-4 w-4',
  })

  return (
    <div className="relative ml-3 border-l-2 border-gray-200 pl-4">
      {events.map((event) => {
        const expandable = !!event.details || (event.toolCalls && event.toolCalls.length > 0)
        const isOpen = isEventOpen(event.id, !!expandable)

        return (
          <div key={event.id} className="relative pb-3">
            {/* Dot on timeline */}
            <div
              className={[
                'absolute rounded-full',
                event.dotSize ?? 'h-3 w-3',
                event.dotClass,
                event.dotSize === 'h-4 w-4' ? '-left-[25px] mt-0.5' : '-left-[24px] mt-1',
              ].join(' ')}
            />

            {/* Content */}
            <button
              onClick={() => expandable && toggle(event.id)}
              aria-expanded={expandable ? isOpen : undefined}
              className={[
                'w-full text-left text-xs',
                expandable ? 'cursor-pointer hover:bg-gray-50 rounded px-1 -mx-1' : 'cursor-default',
              ].join(' ')}
              disabled={!expandable}
            >
              <div className="flex items-center gap-2">
                <span className="font-medium">{event.title}</span>
                {event.phase && (
                  <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-500">{event.phase}</span>
                )}
                {event.isFinal && (
                  <span className="rounded-full bg-green-100 px-1.5 py-0.5 text-[10px] font-medium text-green-700">Final</span>
                )}
                {event.timestamp && <span className="ml-auto text-gray-400 tabular-nums">{event.timestamp}</span>}
                {event.duration && <span className={`text-gray-400${event.timestamp ? '' : ' ml-auto'}`}>{event.duration}</span>}
                {expandable && <span className="text-gray-400 flex-shrink-0">{isOpen ? '\u25B2' : '\u25BC'}</span>}
              </div>
              {event.subtitle && <div className="text-gray-400">{event.subtitle}</div>}
              {event.description && (
                <div className="text-gray-400 italic">{event.description}</div>
              )}
              {event.validationNote && (
                <div className="text-amber-600 italic">{event.validationNote}</div>
              )}
            </button>

            {/* No text response box for tool-only LLM rounds */}
            {event.noTextResponse && (
              <div className="mt-1 rounded bg-gray-50 px-2 py-1.5 text-[11px] text-gray-400 italic">
                No text response — AI deferred to tool calls
              </div>
            )}

            {/* Response preview when collapsed */}
            {!isOpen && event.responsePreview && (
              <div className="mt-1 rounded bg-gray-50 px-2 py-1 text-[11px] font-mono text-gray-400 truncate">
                {event.responsePreview}
              </div>
            )}

            {isOpen && event.details && (
              <pre className="mt-1 max-h-40 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap break-words">
                {event.details}
              </pre>
            )}

            {isOpen && event.toolCalls && event.toolCalls.length > 0 && (
              <div className="mt-1 space-y-1.5">
                {event.toolCalls.map((tc, j) => (
                  <NestedToolCard key={j} tc={tc} />
                ))}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}

function truncateJson(json: string, max = 120): string {
  return json.length > max ? json.slice(0, max) + '\u2026' : json
}

function NestedToolCard({ tc }: { tc: ExtractionToolCall }) {
  const [expanded, setExpanded] = useState(false)
  const summary = getToolSummary(tc)
  const isLong = (tc.inputJson?.length ?? 0) > 120 || (tc.outputJson?.length ?? 0) > 120

  return (
    <div className="rounded bg-gray-50 p-2 text-xs">
      <button
        onClick={() => isLong && setExpanded(!expanded)}
        aria-expanded={isLong ? expanded : undefined}
        className={[
          'flex w-full items-start gap-2 text-left',
          isLong ? 'cursor-pointer' : 'cursor-default',
        ].join(' ')}
      >
        <span className={tc.succeeded ? 'text-green-600' : 'text-red-600'}>
          {tc.succeeded ? '\u2713' : '\u2717'}
        </span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono font-medium">{tc.toolName}</span>
            <span className="text-gray-400">{formatDurationMs(tc.durationMs)}</span>
            {isLong && (
              <span className="ml-auto text-gray-400 flex-shrink-0" aria-hidden="true">{expanded ? '\u25B2' : '\u25BC'}</span>
            )}
          </div>
          {summary && <div className="text-gray-500">{summary}</div>}
          {!expanded && tc.inputJson && (
            <div className="mt-0.5 text-gray-400 truncate">
              Input: {truncateJson(tc.inputJson)}
            </div>
          )}
        </div>
      </button>
      {expanded && (
        <div className="mt-1.5 ml-5 space-y-1">
          {tc.inputJson && (
            <div>
              <div className="text-gray-400 mb-0.5">Input:</div>
              <pre className="max-h-40 overflow-auto rounded bg-white p-1.5 text-xs whitespace-pre-wrap break-words">
                {tc.inputJson}
              </pre>
            </div>
          )}
          {tc.outputJson && (
            <div>
              <div className="text-gray-400 mb-0.5">Output:</div>
              <pre className="max-h-40 overflow-auto rounded bg-white p-1.5 text-xs whitespace-pre-wrap break-words">
                {tc.outputJson}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
