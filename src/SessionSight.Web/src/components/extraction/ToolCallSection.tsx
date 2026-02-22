import { useState } from 'react'
import type { ExtractionToolCall } from '../../types/extractionSteps'
import { ToolCallItem } from './ToolCallItem'

interface ToolCallSectionProps {
  toolCalls: ExtractionToolCall[]
}

export function ToolCallSection({ toolCalls }: ToolCallSectionProps) {
  const [open, setOpen] = useState(false)

  if (toolCalls.length === 0) return null

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
        Tool Calls ({toolCalls.length})
      </button>
      {open && (
        <div className="mt-2 space-y-1">
          {toolCalls.map((tc, i) => (
            <ToolCallItem key={i} toolCall={tc} />
          ))}
        </div>
      )}
    </div>
  )
}
