import { useState } from 'react'
import type { ExtractionToolCall, ExtractionLlmTrace } from '../../types/extractionSteps'
import { buildTimeline } from './mergeTimeline'
import { ToolCallItem } from './ToolCallItem'
import { formatDurationMs } from './stepConfig'

interface ConversationViewProps {
  toolCalls: ExtractionToolCall[]
  traces: ExtractionLlmTrace[]
  defaultOpen?: boolean
}

function CollapsibleBlock({
  label,
  content,
  defaultOpen = false,
  className = '',
}: {
  label: string
  content: string
  defaultOpen?: boolean
  className?: string
}) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className={className}>
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        {label}
        <span className="ml-auto text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <pre className="mt-1 max-h-40 overflow-auto rounded bg-gray-100 p-2 text-xs whitespace-pre-wrap break-words">
          {content}
        </pre>
      )}
    </div>
  )
}

export function ConversationView({ toolCalls, traces, defaultOpen }: ConversationViewProps) {
  const timeline = buildTimeline(toolCalls, traces)

  if (timeline.length === 0 && toolCalls.length === 0) return null

  // Extract system and user segments from round 0
  const round0 = timeline[0]
  const systemSegment = round0?.segments.find((s) => s.role === 'system')
  const userSegment = round0?.segments.find((s) => s.role === 'user')

  return (
    <div className="space-y-3">
      {/* System prompt */}
      {systemSegment && (
        <CollapsibleBlock
          label="System Prompt"
          content={systemSegment.content}
          defaultOpen={defaultOpen}
          className="rounded bg-gray-100 p-2 text-gray-600"
        />
      )}

      {/* User note */}
      {userSegment && (
        <CollapsibleBlock
          label={`Therapy Note (${userSegment.content.split(/\s+/).length} words)`}
          content={userSegment.content}
          defaultOpen={defaultOpen}
          className="rounded bg-gray-100 p-2 text-gray-600"
        />
      )}

      {/* Rounds */}
      {timeline.map((round, i) => {
        const isLast = i === timeline.length - 1
        const toolSegments = round.segments.filter((s) => s.role === 'tool')
        const hasToolCalls = round.toolCalls.length > 0
        const hasResponse = !!round.responseText

        const tokenLabel =
          round.inputTokens > 0 || round.outputTokens > 0
            ? ` \u00B7 ${round.inputTokens} in / ${round.outputTokens} out`
            : ''

        return (
          <div key={round.round}>
            {/* Round divider */}
            <div className="border-b border-gray-100 py-1 text-xs text-gray-400">
              Round {round.round} \u00B7 {formatDurationMs(round.durationMs)}
              {tokenLabel}
            </div>

            <div className="mt-2 space-y-2">
              {/* Tool results returned to AI (from previous round's execution) */}
              {toolSegments.length > 0 && (
                <div className="rounded border-l-2 border-green-300 bg-green-50 p-2">
                  <div className="text-xs font-medium text-green-700 mb-1">
                    Tool results returned to AI:
                  </div>
                  {toolSegments.map((seg, j) => (
                    <pre
                      key={j}
                      className="max-h-32 overflow-auto rounded bg-white/50 p-1.5 text-xs whitespace-pre-wrap break-words mb-1 last:mb-0"
                    >
                      {seg.content}
                    </pre>
                  ))}
                </div>
              )}

              {/* Tool calls executed this round */}
              {hasToolCalls && (
                <div className="rounded border-l-2 border-blue-300 bg-blue-50 p-2">
                  <div className="text-xs font-medium text-blue-700 mb-1">
                    AI called {round.toolCalls.length} tool{round.toolCalls.length !== 1 ? 's' : ''}:
                  </div>
                  <div className="space-y-1">
                    {round.toolCalls.map((tc, j) => (
                      <ToolCallItem key={j} toolCall={tc} />
                    ))}
                  </div>
                </div>
              )}

              {/* AI response */}
              {hasResponse && (
                <ConversationResponseBlock
                  content={round.responseText!}
                  isFinal={isLast && !hasToolCalls}
                  defaultOpen={defaultOpen}
                />
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}

function ConversationResponseBlock({
  content,
  isFinal,
  defaultOpen = false,
}: {
  content: string
  isFinal: boolean
  defaultOpen?: boolean
}) {
  const [open, setOpen] = useState(defaultOpen)
  const label = isFinal ? 'AI response (final)' : 'AI response'

  return (
    <div className="rounded border-l-2 border-gray-300 bg-white p-2">
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        {label}
        <span className="ml-auto text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <pre className="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap break-words">
          {content}
        </pre>
      )}
    </div>
  )
}
