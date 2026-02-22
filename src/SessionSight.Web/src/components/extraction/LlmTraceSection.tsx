import { useState } from 'react'
import type { ExtractionLlmTrace } from '../../types/extractionSteps'
import { LlmTraceItem } from './LlmTraceItem'

interface LlmTraceSectionProps {
  traces: ExtractionLlmTrace[]
  defaultOpen?: boolean
}

export function LlmTraceSection({ traces, defaultOpen = false }: LlmTraceSectionProps) {
  const [open, setOpen] = useState(defaultOpen)

  if (traces.length === 0) return null

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
        LLM Traces ({traces.length})
      </button>
      {open && (
        <div className="mt-2 space-y-1">
          {traces.map((tr, i) => (
            <LlmTraceItem key={i} trace={tr} />
          ))}
        </div>
      )}
    </div>
  )
}
