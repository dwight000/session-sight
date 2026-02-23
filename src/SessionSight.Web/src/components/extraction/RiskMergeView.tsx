import { useState } from 'react'
import type { RiskDiagnostics } from '../../types'
import { formatFieldName } from '../../utils/format'

interface RiskMergeViewProps {
  diagnostics: RiskDiagnostics | null | undefined
  defaultOpen?: boolean
}

export function RiskMergeView({ diagnostics, defaultOpen = false }: RiskMergeViewProps) {
  const [open, setOpen] = useState(defaultOpen)

  if (!diagnostics?.fieldDecisions?.length) return null

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
        Risk Merge ({diagnostics.fieldDecisions.length} fields)
      </button>
      {open && (
        <div className="mt-2 space-y-2">
          {diagnostics.guardrailApplied && (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
              <span className="font-medium">Guardrail Applied</span>
              {diagnostics.homicidalGuardrail?.applied && (
                <span className="ml-2">— Homicidal: {diagnostics.homicidalGuardrail.reason}</span>
              )}
              {diagnostics.selfHarmGuardrail?.applied && (
                <span className="ml-2">— Self-harm: {diagnostics.selfHarmGuardrail.reason}</span>
              )}
            </div>
          )}
          {diagnostics.fieldDecisions.map((decision) => (
            <div key={decision.field} className="rounded border border-gray-200 bg-white p-3 text-xs">
              <div className="flex items-center gap-2">
                <span className="font-medium text-gray-900">{formatFieldName(decision.field)}</span>
                <span className="rounded-full bg-blue-100 px-2 py-0.5 text-blue-700">{decision.ruleApplied}</span>
              </div>
              <div className="mt-2 grid grid-cols-3 gap-2 text-gray-700">
                <div>
                  <p className="font-medium text-gray-500">Original</p>
                  <p className="mt-0.5">{decision.originalValue || '\u2014'}</p>
                </div>
                <div>
                  <p className="font-medium text-gray-500">Re-Extracted</p>
                  <p className="mt-0.5">{decision.reExtractedValue || '\u2014'}</p>
                </div>
                <div>
                  <p className="font-medium text-gray-500">Final</p>
                  <p className="mt-0.5 font-medium">{decision.finalValue || '\u2014'}</p>
                </div>
              </div>
              {decision.criteriaUsed.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1">
                  {decision.criteriaUsed.map((c) => (
                    <span key={c} className="rounded bg-gray-100 px-1.5 py-0.5 text-gray-600">{c}</span>
                  ))}
                </div>
              )}
              {decision.reasoningUsed && (
                <p className="mt-1 text-gray-500 italic">{decision.reasoningUsed}</p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
