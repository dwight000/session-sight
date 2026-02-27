import type { StepViewMode } from '../../types/extractionSteps'

interface ViewModeSelectorProps {
  value: StepViewMode
  onChange: (mode: StepViewMode) => void
}

const MODES: { value: StepViewMode; label: string }[] = [
  { value: 'raw', label: 'Raw' },
  { value: 'conversation', label: 'Conversation' },
  { value: 'activity', label: 'Activity' },
  { value: 'summary', label: 'Summary' },
]

export function ViewModeSelector({ value, onChange }: ViewModeSelectorProps) {
  return (
    <div className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5" role="radiogroup" aria-label="View mode">
      {MODES.map((mode) => (
        <button
          key={mode.value}
          role="radio"
          aria-checked={value === mode.value}
          onClick={() => onChange(mode.value)}
          className={[
            'rounded-md px-3 py-1 text-xs font-medium transition-colors',
            value === mode.value
              ? 'bg-blue-50 text-blue-700'
              : 'text-gray-500 hover:text-gray-700',
          ].join(' ')}
        >
          {mode.label}
        </button>
      ))}
    </div>
  )
}
