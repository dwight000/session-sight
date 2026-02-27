import { useState } from 'react'
import type { ExtractionToolCall } from '../../types/extractionSteps'
import { formatDurationMs } from './stepConfig'

interface ToolCallItemProps {
  toolCall: ExtractionToolCall
}

export function ToolCallItem({ toolCall }: ToolCallItemProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className="border border-gray-100 rounded">
      <button
        onClick={() => setOpen(!open)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-xs hover:bg-gray-50"
      >
        <span>{toolCall.succeeded ? '\u2713' : '\u2717'}</span>
        <span className="font-mono">{toolCall.toolName}</span>
        <span className="text-gray-400">Round {toolCall.loopRound}</span>
        <span className="ml-auto text-gray-400">{formatDurationMs(toolCall.durationMs)}</span>
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
      </button>
      {open && (
        <div className="border-t border-gray-100 px-3 py-2 space-y-2">
          {toolCall.inputJson && (
            <div>
              <p className="text-xs font-medium text-gray-500">Input</p>
              <pre className="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs">{toolCall.inputJson}</pre>
            </div>
          )}
          {toolCall.outputJson && (
            <div>
              <p className="text-xs font-medium text-gray-500">Output</p>
              <pre className="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs">{toolCall.outputJson}</pre>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
