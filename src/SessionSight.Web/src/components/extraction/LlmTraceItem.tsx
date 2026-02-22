import { useState } from 'react'
import type { ExtractionLlmTrace } from '../../types/extractionSteps'
import { formatDurationMs } from './stepConfig'

interface LlmTraceItemProps {
  trace: ExtractionLlmTrace
}

export function LlmTraceItem({ trace }: LlmTraceItemProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className="border border-gray-100 rounded">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-xs hover:bg-gray-50"
      >
        <span className="font-mono">{trace.modelUsed}</span>
        <span className="text-gray-400">Round {trace.loopRound}</span>
        <span className="text-gray-400">{trace.inputTokens} in / {trace.outputTokens} out</span>
        <span className="ml-auto text-gray-400">{formatDurationMs(trace.durationMs)}</span>
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <div className="border-t border-gray-100 px-3 py-2 space-y-2">
          {trace.promptText && (
            <div>
              <p className="text-xs font-medium text-gray-500">Prompt</p>
              <pre className="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap">{trace.promptText}</pre>
            </div>
          )}
          {trace.responseText && (
            <div>
              <p className="text-xs font-medium text-gray-500">Response</p>
              <pre className="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs whitespace-pre-wrap">{trace.responseText}</pre>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
