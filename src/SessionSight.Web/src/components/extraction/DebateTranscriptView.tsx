import { useState } from 'react'
import type { DebateResultSummary } from '../../types/extractionSteps'

interface DebateTranscriptViewProps {
  summary: DebateResultSummary
  defaultOpen?: boolean
}

export function DebateTranscriptView({ summary, defaultOpen = false }: DebateTranscriptViewProps) {
  const [open, setOpen] = useState(defaultOpen)

  if (!summary.rounds || summary.rounds.length === 0) return null

  return (
    <div>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-xs font-medium text-gray-600 hover:text-gray-900"
      >
        <span className="text-gray-400">{open ? '\u25B2' : '\u25BC'}</span>
        Debate Transcript ({summary.rounds.length} round{summary.rounds.length !== 1 ? 's' : ''})
        <span className="text-gray-400 font-normal">
          Advocate: {summary.advocateModel} · Challenger: {summary.challengerModel} · Judge: {summary.judgeModel}
        </span>
      </button>

      {open && (
        <div className="mt-2 space-y-3">
          {summary.rounds.map((round) => (
            <div key={round.roundNumber} className="space-y-1.5">
              <div className="text-[10px] font-medium text-gray-400 uppercase tracking-wide">
                Round {round.roundNumber}
              </div>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <div className="border-l-2 border-indigo-400 pl-3">
                  <div className="text-[10px] font-semibold text-indigo-600 mb-0.5">Advocate</div>
                  <p className="text-xs text-gray-700 whitespace-pre-wrap">{round.advocateArgument}</p>
                </div>
                <div className="border-l-2 border-amber-400 pl-3">
                  <div className="text-[10px] font-semibold text-amber-600 mb-0.5">Challenger</div>
                  <p className="text-xs text-gray-700 whitespace-pre-wrap">{round.challengerArgument}</p>
                </div>
              </div>
            </div>
          ))}

          {/* Judge Synthesis */}
          <div className="border-l-2 border-violet-400 pl-3">
            <div className="text-[10px] font-semibold text-violet-600 mb-0.5">Judge Synthesis</div>
            <pre className="text-xs text-gray-700 whitespace-pre-wrap max-h-40 overflow-auto">
              {summary.judgeSynthesis}
            </pre>
          </div>

          {/* Review Reasons */}
          {summary.reviewReasons && summary.reviewReasons.length > 0 && (
            <div className="rounded bg-amber-50 border border-amber-200 px-3 py-2">
              <div className="text-[10px] font-semibold text-amber-700 mb-1">Review Reasons</div>
              <ul className="list-disc list-inside text-xs text-amber-800 space-y-0.5">
                {summary.reviewReasons.map((reason, i) => (
                  <li key={i}>{reason}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
